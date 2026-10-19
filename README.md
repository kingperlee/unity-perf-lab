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

## 目录

```text
Project/
  Assets/PerfLab/Runtime/Benchmarking/   通用基准测试框架
  Assets/PerfLab/Runtime/Samples/        框架校准与后续实验
  Packages/                 Unity 包清单
  ProjectSettings/          Unity 工程版本与设置
```
