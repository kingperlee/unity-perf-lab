namespace PerfLab.Benchmarking
{
    public enum BenchmarkState
    {
        Idle,
        Calibrating,
        SettingUp,
        WarmingUp,
        Sampling,
        Finalizing,
        Completed,
        Failed
    }
}
