namespace PerfLab.Benchmarking
{
    public interface IBenchmarkScenario
    {
        string Name { get; }

        BenchmarkMeasurementMode MeasurementMode { get; }

        void Setup(BenchmarkConfiguration configuration);

        void ExecuteStep();

        void Cleanup();
    }
}
