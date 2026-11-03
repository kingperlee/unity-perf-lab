# Unity Performance Lab

一个用于学习、验证和展示 Unity 性能优化方法的实验室。本工程专注于可重复的规模化性能实验；生命周期、对象池等基础工程主题由 `unity-core-lab` 展示，这里不重复实现。

## Benchmark Harness

当前首先建设统一的基准测试框架。所有后续实验都使用同一条执行管线：

```text
Setup → Warmup（不记录）→ Sampling → Statistics → Cleanup → Export
```

第一版框架提供：

- 明确且不可重入的运行状态；
- 固定随机种子和结构化测试参数；
- 预热与正式样本分离；
- 仅包围被测工作负载的 `Stopwatch` 计时；
- 当前线程托管分配测量；
- Min、Mean、P50、P95、P99、Max 统计；
- 异常时仍执行清理；
- 带 Unity、平台和设备信息的 JSON 导出。

`ArithmeticBenchmarkScenario` 是用于验证框架流程的校准负载，不代表真实性能优化结论。

## 实验 01：Update 分发规模曲线

打开 `Assets/PerfLab/Scenes/UpdateDispatchDemo.unity`：

- `1`：每个对象通过自己的 `MonoBehaviour.Update` 执行一次相同计算；
- `2`：只有一个管理器接收 `Update`，再顺序遍历所有对象；
- `[` / `]`：在 1,000、5,000、10,000、20,000 个对象之间切换；
- `E`：导出上一次结果。

两种模式具有相同数量的 GameObject、组件和计算步骤。Harness 隐藏采样期 IMGUI，预热 120 帧并采样 300 帧，记录相邻帧间隔及每帧 GC。请先后运行两种模式并比较相同对象数量下的 P50/P95/P99；Frame Interval 代表玩家感受到的帧间时长，不等于被测 Update 的独占 CPU 时间。Editor 结果只用于学习，最终结论应由目标设备 Development Build 复核。

## 实验 02：托管分配来源

打开 `Assets/PerfLab/Scenes/GcAllocationDemo.unity`：

- `0`：普通循环零分配基线；
- `1`：值类型装箱；
- `2`：每次迭代创建捕获闭包；
- `3`：LINQ Where/Select/Sum 管线；
- `4`：循环构造字符串；
- `[` / `]`：切换每个样本执行 100、1,000 或 10,000 次操作；
- `E`：导出上一次结果。

该实验使用 `MeasuredStep`：Stopwatch 只包围被测方法。Harness 会先探测当前运行时是否支持 `GC.GetAllocatedBytesForCurrentThread`；支持时记录精确的方法内当前线程分配，不支持时退回 Unity 的 `GC Allocated In Frame`。退回时先丢弃 10 个 Recorder 基线预热帧，再采集 30 个空负载基线帧；JSON 同时保存 `rawAllocatedBytes`、`allocationBaselineBytes` 和扣除基线后的 `allocatedBytes`，不会丢失原始证据。界面及 JSON 还会记录计数器来源和三个原始探测值。不同案例用于定位各自的分配来源，不应把它们的耗时视为完全相同业务工作的横向排名。

正式结果采集 300 个样本。P50 表示典型值，P95 表示 95% 样本不超过该值，P99 用于观察最慢或分配最高的尾部 1%；百分位使用线性插值，所以字节统计也可能显示小数。

## 实验 03：Physics 查询与 NonAlloc

打开 `Assets/PerfLab/Scenes/PhysicsQueryDemo.unity`：

- `0`：只返回最近命中的 `Physics.Raycast`；
- `1` / `2`：比较 `RaycastAll` 与预分配缓冲区的 `RaycastNonAlloc`；
- `3` / `4`：比较 `OverlapSphere` 与预分配缓冲区的 `OverlapSphereNonAlloc`；
- 左右方括号：切换每个样本执行 100、500 或 1,000 次查询；
- `M`：切换查询所有层或仅查询目标层；
- `E`：导出结果。

场景为每种模式创建相同的 256 个 BoxCollider，其中一半位于目标层、一半作为干扰层。NonAlloc 缓冲区容量大于最大可能命中数，避免结果截断伪装成性能提升。所有查询都会累计命中数，防止无消费结果的无效比较。

## 实验 04：Jobs/Burst 工作量交叉点

打开 `Assets/PerfLab/Scenes/JobsBurstDemo.unity`：

- `0`：主线程直接遍历 `NativeArray`；
- `1` / `2`：比较普通 `IJob` 与 Burst 编译的 `IJob`；
- `3` / `4`：比较普通与 Burst 编译的 `IJobParallelFor`；
- 左右方括号：切换 1,000、10,000、100,000 或 1,000,000 个元素；
- `E`：导出结果。

五种模式使用相同数据和 16 轮浮点计算。计时有意包含 `Schedule()` 到紧随其后的 `Complete()`：它测量的是“本帧立即需要结果”时的端到端成本，小工作量下调度和同步成本可能高于并行收益，数据规模增大后才可能出现交叉点。实际业务如果允许延迟，应尽早 Schedule、尽晚 Complete，或在后续帧检查 `JobHandle.IsCompleted` 后 Complete 并刷新结果，让主线程工作与 Job 重叠；这属于另一种流水线测试，不能与本实验结果混为一谈。`NativeArray` 在 Setup 阶段一次性分配并在 Cleanup 释放，不把容器创建成本混入样本。请关闭 Burst Safety Checks 后再做最终性能结论，并用目标设备 Development Build 复核；Editor 数值主要用于观察趋势。

## 实验 05：纹理准备、上传与尖峰

打开 `Assets/PerfLab/Scenes/TextureUploadDemo.unity`：

- `0`：只在 CPU 填充纹理原始像素；
- `1`：只修改一个像素后 `Apply`，不重建 Mipmap；
- `2`：`Apply` 并重建 Mipmap；
- `3`：完整填充像素后再 `Apply`；
- 左右方括号：切换 256、512、1024、2048 边长；
- `E`：导出结果。

本实验先把 CPU 数据准备、纹理提交和 Mipmap 重建拆开，纹理在 Setup 创建并复用，避免把对象创建混入样本。Measured Step 的 Stopwatch 只表示主线程准备及提交所花的时间；图形 API 可能异步执行，因此它不能证明 GPU 已经完成上传。GPU 完成时间、渲染线程等待及显存行为需要结合 Unity Profiler、Render Thread/GPU 模块和目标设备验证。首次资源加载与缓存命中的对照将在本阶段后续加入，不能通过反复 `Resources.Load` 同一资源来模拟首次加载。

## 实验 06：AoS、SoA 与热/冷数据

打开 `Assets/PerfLab/Scenes/DataLayoutDemo.unity`：

- `0` / `1`：比较紧凑的 24 字节 AoS 与六个连续数组的 SoA；两者访问全部位置和速度字段并执行相同计算；
- `2` / `3` / `4`：使用完全相同的 X 位置与 X 速度计算，依次比较 24 字节紧凑 AoS、64 字节宽 AoS及热/冷分离 SoA；
- 左右方括号：切换 10,000、100,000 或 1,000,000 个元素；
- `E`：导出结果。

第一组用于说明 SoA 并非天然更快：当每个对象的所有紧凑字段都会一起使用时，AoS 也有良好局部性。第二组先用紧凑 AoS 建立相同热计算的控制基线，再把每个 AoS 元素扩展到典型桌面 CPU 的 64 字节缓存行；SoA 仍分配相同逻辑冷字段，却让热循环只顺序扫描两个 `float[]`。三者计算完全一致，用于分别观察结构步长、无用数据进入缓存和热/冷分离的影响。数组仅在 Setup 分配，采样路径不创建对象；最终结论仍需在目标设备和对应脚本后端复核。

## 实验 07：Transform 层级与结构变更

打开 `Assets/PerfLab/Scenes/TransformHierarchyDemo.unity`：

- `0` / `1`：移动同一个根节点，再读取所有节点世界坐标；比较平坦叶节点与固定深度 16 的链；
- `2`：逐个修改平坦叶节点的本地坐标，再读取世界坐标；
- `3`：逐个把叶节点切换到另一个父节点，再读取世界坐标；
- 左右方括号：切换 1,000、5,000 或 10,000 个 Transform；
- `E`：导出结果。

所有模式使用相同节点数量，采样计时包含写入和随后读取世界坐标的成本。读取结果用于消费并触发所需的世界矩阵更新，避免把“只标记为脏、真正计算留到以后”误认为完整成本。深层级会增加祖先依赖传播；`SetParent` 还属于结构性变更，可能涉及父子列表维护及局部/世界变换处理，因此不应与普通位置赋值等同。场景只测 Transform，不附加 Renderer、Collider 或脚本组件。

## 实验 08：FixedUpdate 追帧与螺旋恶化

打开 `Assets/PerfLab/Scenes/FixedTimestepDemo.unity`：

- 左右方括号：切换 20、10 或 5 ms 的固定时间步（50、100 或 200 Hz）；
- `M`：切换约 333 ms 和 50 ms 的 `Time.maximumDeltaTime`；
- `W`：切换每次 `FixedUpdate` 的模拟 CPU 工作量；
- `1` / `2` / `3`：故意阻塞主线程 50、100 或 250 ms；
- `R`：清除单个渲染帧内 FixedUpdate 次数峰值。

界面统计进入当前 `Update` 前实际执行的 `FixedUpdate` 次数。主线程发生停顿后，Unity 会尝试用连续多次固定步追赶累计的模拟时间；固定时间步越短，需要补做的步数越多。如果每一步本身又很昂贵，追赶工作会继续拉长帧时间，形成“掉帧 → 补更多物理步 → 更严重掉帧”的螺旋恶化。`maximumDeltaTime` 会限制一次长帧可用于追赶的时间，但代价是游戏模拟时间相对真实时间变慢。本实验用 `Thread.Sleep` 作为明确的故障注入，不代表推荐在游戏循环中主动休眠。

## 实验 09：资源缓存、实例化与对象池

打开 `Assets/PerfLab/Scenes/ResourceLifecycleDemo.unity`：

- `0`：卸载 Unity 的 `TextAsset` 对象并等待 `UnloadUnusedAssets`，随后计时一次同步 `Resources.Load`；
- `1`：对已经缓存的资源执行 10,000 次 `Resources.Load` 查询；
- `2`：实例化并激活 500 个带 8 个子节点的内存模板；销毁请求放在计时区间之后；
- `3`：预热 500 个相同对象，然后只计时从池中启用和归还的成本。

模式 0 只建立“Unity 对象未加载”和“Unity 对象已缓存”的区别，不能清除操作系统文件缓存，因此不能代表机器重启后的物理磁盘冷读。模式 1 展示缓存命中仍有资源路径查询成本。模式 2 把内存中 Prefab 式克隆和层级激活视为初始化成本，同时避免把 Unity 延迟到帧末处理的 `Destroy` 假装成完整释放耗时。模式 3 的预热不计时，只回答运行期复用是否能把创建尖峰移到加载阶段。首次运行和 Editor 结果仍应在目标平台 Player 中复核。

## 实验 10：批量工作与分帧预算

打开 `Assets/PerfLab/Scenes/FrameBudgetingDemo.unity`：

- `0`：在一个帧切片内创建并激活 2,000 个对象；
- `1`：把相同创建工作按每帧 10、50 或 200 个拆分；
- `2`：在一个帧切片内提交全部 `Destroy` 请求；
- `3`：把相同销毁请求按批次拆分；
- 左右方括号：调整每帧批量大小。

每个对象含 4 个子节点。界面分别报告累计主动工作时间、最重的单帧工作切片和运行期间最大的帧间隔。分帧通常不会消除总 CPU 工作，甚至会增加循环与调度开销；它用更长的完成延迟换取更小的单帧尖峰。`Destroy` 只在调用时标记对象，Unity 的原生对象清理发生在稍后阶段，因此本实验额外保留后续帧并观察帧间隔，避免只测 `Destroy()` 调用就得出“释放很便宜”的结论。

## 实验 11：Transform 与物理世界同步

打开 `Assets/PerfLab/Scenes/PhysicsTransformSyncDemo.unity`：

- `0`：先移动全部 Collider Transform，显式调用一次 `Physics.SyncTransforms()`，再执行全部 Raycast；
- `1`：对每个 Collider 依次执行移动、显式同步、Raycast；
- `2`：启用 `Physics.autoSyncTransforms`，对每个 Collider 交错执行移动和 Raycast；
- 左右方括号：切换 100、500 或 1,000 个 Collider；
- `E`：导出结果。

三种模式每个样本执行相同数量的 Transform 写入与 Raycast，区别是能否把物理世界同步合并。先集中修改再同步一次，物理引擎可以批量更新 broadphase；如果业务逻辑要求每移动一个对象就立即查询其最新物理状态，同步工作会被重复触发。`autoSyncTransforms` 提供的是兼容旧行为的正确性便利，不表示同步成本消失。测试会在 Cleanup 恢复工程原来的全局设置。

## 实验 12：托管分配与垃圾回收暂停

打开 `Assets/PerfLab/Scenes/GcBehaviorDemo.unity`：

- `Space`：持续在每帧制造短生命周期垃圾；
- 左右方括号：切换每帧 32 KiB、256 KiB 或 1 MiB；
- `B`：在当前帧突发制造 16 MiB 临时垃圾；
- `L`：持有额外 16 MiB 引用；`U`：只释放这些引用；
- `C`：显式执行完整 `GC.Collect` 流程；
- `R`：重置回收次数及帧间隔观察；
- `H`：隐藏或恢复 IMGUI，便于采集更干净的 Profiler 数据。

分配、失去引用和真正回收是三个不同事件。`List.Clear()` 只会让对象具备被回收的资格，不会保证内存立即下降；回收发生时才扫描并处理不可达对象。界面记录各代 `GC.CollectionCount`、报告的托管内存及最大帧间隔，但 Unity 使用的 GC 实现和增量模式会影响代际计数及暂停形态。显式完整回收只用于展示停顿，不应被当成常规性能优化。IMGUI 自身可能产生分配，正式观察时应按 `H` 隐藏界面并配合 Profiler。

## 实验 13：Job 立即等待与跨帧流水线

打开 `Assets/PerfLab/Scenes/JobsPipelineDemo.unity`：

- `0`：停止持续调度；
- `1`：每帧 Schedule Burst `IJobParallelFor` 后立刻 `Complete()`；
- `2`：调度后跨帧轮询 `IsCompleted`，只在完成后调用 `Complete()`、读取结果并调度下一份工作；
- 左右方括号：切换 100,000、500,000 或 1,000,000 个元素；
- `R`：重置吞吐、延迟和帧间隔观察。

立即 `Complete` 能在本帧得到最新结果，但 `Complete()` 会包含尚未完成的工作线程时间，主线程因此阻塞。跨帧模式不会读取正在被 Job 写入的 `NativeArray`；它先检查 `IsCompleted`，确认完成后仍调用 `Complete()` 以结束依赖和安全状态，再发布结果。这样可以让主线程与 Job 重叠，但结果会延迟一个或多个渲染帧，吞吐量也受到“只允许一个在途 Job”的限制。真实系统可进一步使用双缓冲和依赖链，在不读写冲突的前提下保持多份工作流水化。

## 实验 14：引用缓存与查找范围

打开 `Assets/PerfLab/Scenes/ReferenceLookupDemo.unity`：

- `0` / `1` / `2`：比较缓存组件引用、`GetComponent<T>` 与 `TryGetComponent<T>`；目标组件排在 8 个占位组件之后；
- `3` / `4`：比较缓存子 Transform 与在 64 个直接子节点中按名称执行 `Transform.Find`；
- `5`：按包含 8 层的路径执行 `Transform.Find`；
- `6`：在包含 1,000 个干扰对象的活动场景中执行 `GameObject.Find`；
- 左右方括号：切换每个样本 100、1,000 或 10,000 次操作；
- `E`：导出结果。

模式 0–2 属于同一 GameObject 的组件表访问，模式 3–5 属于局部层级访问，可以分别横向比较。`GameObject.Find` 是全活动场景名称搜索，语义和搜索范围都不同，只用于展示把它放进热路径的规模风险，不能被解释为 `GetComponent` 的直接替代方案。缓存引用通常最快，但需要处理对象销毁、场景切换和池复用后的引用有效性；不应为了省一次低频查找引入错误的生命周期所有权。

## 实验 15：UGUI 脏标记与重建范围

打开 `Assets/PerfLab/Scenes/UiRebuildDemo.unity`：

- `0`：只修改普通数据，作为不触发 UI 系统的控制基线；
- `1`：修改一个 `Image.color` 并强制完成 Canvas 更新；
- `2`：修改一个 `LayoutElement.preferredHeight`，强制重建包含全部元素的布局并完成 Canvas 更新；
- `3`：修改相同属性，但动态布局根和 Canvas 中只有 50 个元素，其余元素位于独立静态 Canvas；
- 左右方括号：切换总计 100、500 或 1,000 个 UI 元素；
- `E`：导出结果。

UGUI 属性赋值经常只设置脏标记，真正的 Layout、Graphic 和 Canvas 批次更新发生在稍后的 PlayerLoop 阶段；只用 Stopwatch 包围赋值会漏掉主要成本。本实验使用 `LayoutRebuilder.ForceRebuildLayoutImmediate` 和 `Canvas.ForceUpdateCanvases`，有意把完整工作拉进测量区间。业务代码通常仍应使用延迟合并的正常重建，而不是每次修改后强制刷新。独立 Canvas 可限制 Graphic rebuild 范围；要限制 Layout 传播，还需要拆分布局根，并权衡额外 Canvas 的批次和管理成本。

## 实验 16：字符串标识符与整数 ID 缓存

打开 `Assets/PerfLab/Scenes/PropertyIdDemo.unity`：

- `0` / `1`：比较 `Material.SetColor(string, value)` 与预缓存属性 ID 的 `SetColor(int, value)`；
- `2` / `3`：比较每次调用 `Shader.PropertyToID` 与直接复用缓存 ID；
- `4` / `5`：比较每次调用 `Animator.StringToHash` 与直接复用缓存 hash；
- 左右方括号：切换每个样本 1,000、10,000 或 100,000 次操作；
- `E`：导出结果。

字符串字面量本身会被驻留，重复传入并不等于每次创建新的托管字符串；需要避免的是热路径中重复执行名称解析、哈希以及 string 重载的跨边界工作。`Shader.PropertyToID` 和 `Animator.StringToHash` 适合在 `static readonly` 字段或初始化阶段执行一次。缓存值只在相应 API 语义中使用，不应假设 Shader 属性 ID 与 Animator 参数 hash 可以互换。

## 实验 17：数据标志、组件启用与层级激活

打开 `Assets/PerfLab/Scenes/ActivationDemo.unity`：

- `0`：为每个逻辑节点切换普通 `bool`；
- `1`：逐个禁用和启用 `MonoBehaviour.enabled`；
- `2`：逐个对平坦子对象调用 `SetActive(false/true)`；
- `3`：只调用一次父节点 `SetActive(false/true)`，让相同数量的后代随层级切换；
- 左右方括号：切换 100、1,000 或 5,000 个节点；
- `E`：导出结果。

四种模式都让相同数量的逻辑节点经历关闭和开启。普通字段只修改托管数据；`Behaviour.enabled` 还会改变 Unity 的回调状态；`SetActive` 会计算层级中的 `activeInHierarchy` 并触发 `OnDisable/OnEnable`。父节点切换减少了 C# 到原生层的 API 调用次数，但引擎仍需遍历每个受影响的后代。示例节点只带一个轻量探针；真实对象如果还有 Renderer、Collider、Animator 等组件，还会产生各子系统的注销和重新注册成本。

## 实验 18：关闭日志后的参数构造成本

打开 `Assets/PerfLab/Scenes/LoggingDemo.unity`：

- `0`：只执行相同规模的基线循环；
- `1`：关闭 Unity 日志输出，但仍为每次 `Debug.Log` 预先拼接字符串；
- `2`：在调用点检查日志开关，关闭时不构造拼接字符串；
- `3`：关闭输出后调用 `Debug.LogFormat` 的 `params` 重载；
- `4`：在调用点检查开关，关闭时不准备格式化参数；
- 左右方括号：切换每个样本 100、1,000 或 10,000 次操作；
- `E`：导出结果。

C# 会先计算方法实参，再进入 `Debug.Log` 或 `Debug.LogFormat` 检查日志是否启用。因此，关闭输出并不会自动消除调用点的字符串拼接；`params object[]` 还可能创建数组并装箱整数。调用前判断开关才会跳过这些准备工作。实验故意不连续输出真实日志，因为启用后的 Console、堆栈采集、Editor 界面和磁盘写入会主导结果，也可能让编辑器失去响应。

## 综合案例 01：大量怪物名字与血条

目标是在同一画面显示大量怪物 HUD，并逐步从 World-space Canvas 演进到批量 GPU 渲染，同时保持墙体遮挡和 HUD 之间的前后遮挡正确。当前完成第 4 步，打开 `Assets/PerfLab/Scenes/MassHudCaseStudy.unity`：

工程从本阶段起统一使用 URP 14 和 Universal Forward Renderer，并启用 SRP Batcher。迁移前 Built-in Render Pipeline 的历史结果只用于保留学习过程，不能与 URP 结果直接判定性能回归。

- `0`：每个 HUD 拥有自己的 World-space Canvas 和 `Update`，并重复写入没有变化的名字；
- `1`：保持完全相同的 Canvas 层级和画面，由一个 Manager 集中更新，名字只在初始化时赋值；
- `2`：保持相同位置、血量动画和深度卡片，使用共享 Sprite 的 SpriteRenderer 血条和缓存的 3D TextMesh 名字；
- `3`：保留相同的 3D TextMesh 名字成本，把深度卡片、背景、底条和动态血量填充改成显式 GPU Instancing；
- `4`：保持矩形实例化，把多语言名字预热进运行时字体图集，再将每个可见字形作为共享 Quad 的实例绘制；
- `5`：复用一份无每帧分配的 CPU 视锥结果，将屏幕外 HUD 从矩形和字形实例缓冲中一并剔除；
- `6`：保持全部 HUD 可见，将静态矩形改为相机旋转变化时更新，并按距离把血量动画错峰为每 1、4 或 12 帧更新；
- `7`：保留模式 6 的更新策略，用一个复合 Shader/Quad 生成背景、血条底板和填充，替代三层透明矩形；
- `8`：在模式 7 上持续生成池化伤害飘字，固定槽位保存生命周期，活跃数字批量写入预分配字形实例数组；
- 左右方括号：切换 100、500 或 1,000 个 HUD；
- `H`：隐藏 IMGUI，以便使用 Profiler 获取更干净的数据；
- `E`：导出最后一次完整帧间隔结果。
- 相机使用 `WASD` 移动、`Q/Space` 降低/升高、按住鼠标右键旋转、`Shift` 加速、`R` 恢复初始位置。

每个 HUD 后方都有一张只写深度、不写颜色的卡片。场景不透明物体先写深度，近处 HUD 卡片也会阻止远处 HUD 通过深度测试，因此墙体和 HUD 之间都能形成前后遮挡。模式 0 和 1 的 Renderer 与 Canvas 数量完全相同，只比较分散回调、重复文字赋值与集中脏更新。模式 2 开始移除独立 Canvas，仍保留每个 HUD 的 SpriteRenderer、TextMesh 和深度 Renderer，从而隔离 Canvas 重建与独立 Renderer 管理成本。模式 3 从 C# 为深度、背景、底条和填充各发起一次逻辑实例化提交；Unity 允许每次传入最多 1,023 个实例，但渲染后端仍可按平台常量缓冲等限制拆成更小的实际 Instanced Draw。本机 1000 HUD 的 Frame Debugger 结果为 2 次不透明加 6 次透明实例化绘制。名字仍保持每个玩家一个 TextMesh，用来明确显示剩余的文字渲染瓶颈。

模式 4 交替使用拉丁、中文、俄文、日文和韩文名字，启动时一次性请求本轮所需字符；系统字体没有对应字形时回退到 `?`。字形 UV 和实例分组只在初始化时建立，相机旋转变化时才重算 billboard 矩阵。这个示例聚焦图集缓存与提交次数，不实现阿拉伯文连写、双向文本等复杂塑形；生产项目应把塑形结果作为字形实例输入，而不是自行逐个 Unicode 字符排版。

模式 5 不按距离裁掉 HUD，而是每帧用复用的六个视锥平面检查 HUD 锚点，并把可见项紧凑写入已有实例数组。因而相机外的矩形和名字不再提交；相机内无论远近仍会显示，并继续由深度测试决定墙体遮挡和 HUD 间遮挡。

模式 6 不减少可见实例和 Draw 数量，而是减少 CPU 数据更新：深度卡片、背景和血条底板只在相机旋转改变时重建矩阵；动态填充按距离使用 1、4、12 帧三个更新间隔，并用 HUD 索引错开更新相位。它用于展示“全都可见”时，更新频率 LOD 相比逐帧重算全部矩阵的收益及动画流畅度代价。

模式 7 把模式 6 的背景、红色血条底板和绿色填充合并到一个面片。每个实例只上传归一化血量，片元 Shader 根据面片 UV 判断血条区域并选择颜色。深度卡与多语言字形仍然独立绘制，因此画面和遮挡规则保持一致，同时减少两组透明实例绘制、矩阵数据和重叠像素处理。

模式 8 使用 256 个固定伤害事件槽位，以每秒固定速率生成伤害数字。数字布局直接由整数除法完成，不调用 `ToString`，不创建飘字 GameObject，也不在过期时销毁对象。每帧只将仍活跃的数字紧凑写入预分配的矩阵与 UV 数组，然后作为共享字体图集的字形实例绘制；生命周期结束后槽位直接覆盖复用。

## 运行

1. 用 Unity Hub 打开 `Project` 目录。
2. 打开 `Assets/PerfLab/Scenes/BenchmarkHarnessDemo.unity` 并进入 Play Mode。
3. 按 `Space` 运行一次 Harness 校准测试。
4. 测试结束后按 `E` 导出结果。Editor 输出到 `Project/BenchmarkResults`；Player 构建输出到 `Application.persistentDataPath/Benchmarks`。

工程脚本避免使用较新的语法和 API，目标是兼容 Unity 2022.3 LTS 及 Unity 6。若本机安装的是其他编辑器版本，Unity Hub 可在首次打开时升级 `ProjectVersion.txt`。

## 后续阶段

1. 综合 CPU 帧循环：从未优化实现逐级收敛到紧凑热数据；
2. 渲染与 GPU：提交、批处理、Overdraw、RenderTexture 和同步点；
3. 加载与内存：异步加载、资源驻留、峰值内存和场景切换；
4. 真机回归：Player 自动运行、Profiler 采样、预算阈值与基线比较。

## 性能预算与回归判定

`BenchmarkConfiguration.budget` 可以为一次基准配置可选的 P50、P95、P99 毫秒上限及 P95 托管分配上限。Harness 在统计完成后把 `budgetEvaluation.configured`、`passed` 和逐项 `failures` 写入 JSON。所有上限默认值为 `-1`，表示不参与判定；未配置预算的结果仍然是有效测量，但不会被武断地标记为失败。

预算应按目标平台、设备档位、画质和测试场景分别制定。例如 60 FPS 的整帧总预算约为 16.67 ms，但单个系统只能使用其中一部分；`MeasuredStep` 的方法耗时也不能直接拿整帧预算套用。下一步将在同环境基线对比和批处理运行器中使用这些判定结果。

### 自动测试清单

打开 `Assets/PerfLab/Scenes/AutomatedBenchmarkSuite.unity`，在 Editor 中按 `Space`，即可顺序运行一组固定规模的 Update、GC、Physics、Jobs/Burst 和纹理测试。Player 加载该场景时会自动开始，也可以使用 `-benchmarkAutoRun` 命令行标记明确启用自动运行。

每个测试仍导出独立 JSON，结束后额外生成 `*-suite-summary.json`，记录场景名称、P50/P95/P99、预算状态、结果路径和异常信息。Editor 输出到 `Project/BenchmarkResults`，Player 输出到 `Application.persistentDataPath/Benchmarks`。当前清单用于建立统一运行入口，并未预置目标设备预算；在不同设备上获得稳定基线后，再为相应设备档位填写阈值。

在相同 Editor 环境连续运行清单至少两次后，可以使用 `PerfLab > Benchmarks > Compare Latest Suite Summaries` 对比最近两份汇总。工具输出各场景 P50/P95/P99 的相对变化百分比；Unity 版本、平台、CPU、GPU/API、分辨率或画质不一致时，报告会标记为不可比较，避免把设备差异误判成代码回归。当前比较器只报告变化，不使用任意百分比自动判失败。

自动化环境启动 Benchmark Player 时可以附加 `-benchmarkAutoRun -benchmarkQuit`。清单写完所有独立结果和汇总后会自动退出：退出码 `0` 表示所有场景执行成功且已配置预算均通过，退出码 `2` 表示执行失败或预算超标。只传 `-benchmarkAutoRun` 或普通手动启动不会因测试结束而关闭 Player。

使用 `PerfLab > Benchmarks > Build Benchmark Player` 可以为当前激活平台构建只包含自动清单场景的 Player。批处理环境使用 `-executeMethod PerfLab.Editor.BenchmarkPlayerBuild.BuildFromCommandLine`；可通过 `-benchmarkBuildPath <path>` 指定输出，通过 `-benchmarkDevelopmentBuild` 显式生成 Development Build。默认生成非 Development 构建，避免 Profiler 和调试功能干扰正式基线。

## 目录

```text
Project/
  Assets/PerfLab/Runtime/Benchmarking/   通用基准测试框架
  Assets/PerfLab/Runtime/Samples/        框架校准与后续实验
  Packages/                 Unity 包清单
  ProjectSettings/          Unity 工程版本与设置
```
