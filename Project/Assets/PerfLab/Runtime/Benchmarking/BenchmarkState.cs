namespace PerfLab.Benchmarking
{
    public enum BenchmarkState
    {
        Idle,
        SettingUp,
        WarmingUp,
        Sampling,
        Finalizing,
        Completed,
        Failed
    }
}
