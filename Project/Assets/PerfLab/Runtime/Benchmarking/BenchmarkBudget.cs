using System;

namespace PerfLab.Benchmarking
{
    [Serializable]
    public sealed class BenchmarkBudget
    {
        public double maxP50Milliseconds = -1d;
        public double maxP95Milliseconds = -1d;
        public double maxP99Milliseconds = -1d;
        public double maxP95AllocatedBytes = -1d;

        public bool IsConfigured =>
            maxP50Milliseconds >= 0d ||
            maxP95Milliseconds >= 0d ||
            maxP99Milliseconds >= 0d ||
            maxP95AllocatedBytes >= 0d;

        public void Validate()
        {
            ValidateValue(maxP50Milliseconds, nameof(maxP50Milliseconds));
            ValidateValue(maxP95Milliseconds, nameof(maxP95Milliseconds));
            ValidateValue(maxP99Milliseconds, nameof(maxP99Milliseconds));
            ValidateValue(maxP95AllocatedBytes, nameof(maxP95AllocatedBytes));
        }

        public BenchmarkBudget Clone()
        {
            return new BenchmarkBudget
            {
                maxP50Milliseconds = maxP50Milliseconds,
                maxP95Milliseconds = maxP95Milliseconds,
                maxP99Milliseconds = maxP99Milliseconds,
                maxP95AllocatedBytes = maxP95AllocatedBytes
            };
        }

        private static void ValidateValue(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1d)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
