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
