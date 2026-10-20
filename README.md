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

## 运行

1. 用 Unity Hub 打开 `Project` 目录。
2. 打开 `Assets/PerfLab/Scenes/BenchmarkHarnessDemo.unity` 并进入 Play Mode。
3. 按 `Space` 运行一次 Harness 校准测试。
4. 测试结束后按 `E` 导出结果。Editor 输出到 `Project/BenchmarkResults`；Player 构建输出到 `Application.persistentDataPath/Benchmarks`。

工程脚本避免使用较新的语法和 API，目标是兼容 Unity 2022.3 LTS 及 Unity 6。若本机安装的是其他编辑器版本，Unity Hub 可在首次打开时升级 `ProjectVersion.txt`。

## 学习路线

1. Benchmark Harness（当前）
2. `Update` 分发、集中式更新与数据布局
3. 托管分配：装箱、闭包、LINQ 与字符串
4. 物理查询：NonAlloc API、LayerMask 与规模曲线
5. Jobs/Burst 的工作量交叉点
6. 资源加载、GPU 上传与单帧尖峰
7. 真机采样、性能预算与自动回归

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
