using PerfLab.Benchmarking;

namespace PerfLab.Samples
{
    internal sealed class ArithmeticBenchmarkScenario : IBenchmarkScenario
    {
        private int _operations;
        private uint _state;
        private uint _checksum;

        public string Name => "Harness-Arithmetic-Calibration";

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _operations = configuration.operationsPerStep;
            _state = unchecked((uint)configuration.randomSeed);
            _checksum = 0;
        }

        public void ExecuteStep()
        {
            uint value = _state;
            for (var i = 0; i < _operations; i++)
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                _checksum += value;
            }
            _state = value;
        }

        public void Cleanup()
        {
            // Reading the checksum keeps the deterministic workload observable
            // without logging or allocating inside the measured path.
            if (_checksum == uint.MaxValue) _state = _checksum;
        }
    }
}
