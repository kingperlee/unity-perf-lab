using System;

namespace PerfLab.Benchmarking
{
    [Serializable]
    public sealed class BenchmarkConfiguration
    {
        public int warmupFrames = 60;
        public int allocationBaselineWarmupFrames = 10;
        public int allocationBaselineFrames = 30;
        public int sampleFrames = 300;
        public int operationsPerStep = 10000;
        public int randomSeed = 12345;

        public void Validate()
        {
            if (warmupFrames < 0) throw new ArgumentOutOfRangeException(nameof(warmupFrames));
            if (allocationBaselineWarmupFrames < 0) throw new ArgumentOutOfRangeException(nameof(allocationBaselineWarmupFrames));
            if (allocationBaselineFrames < 0) throw new ArgumentOutOfRangeException(nameof(allocationBaselineFrames));
            if (sampleFrames <= 0) throw new ArgumentOutOfRangeException(nameof(sampleFrames));
            if (operationsPerStep <= 0) throw new ArgumentOutOfRangeException(nameof(operationsPerStep));
        }
    }
}
