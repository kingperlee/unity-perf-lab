using System;

namespace PerfLab.Benchmarking
{
    public static class BenchmarkStatistics
    {
        public static BenchmarkDistribution Create(long[] samples, double scale)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(samples));
            }

            var sorted = (long[])samples.Clone();
            Array.Sort(sorted);
            double total = 0d;
            for (var i = 0; i < sorted.Length; i++) total += sorted[i] * scale;

            return new BenchmarkDistribution
            {
                minimum = sorted[0] * scale,
                mean = total / sorted.Length,
                p50 = Percentile(sorted, 0.50d) * scale,
                p95 = Percentile(sorted, 0.95d) * scale,
                p99 = Percentile(sorted, 0.99d) * scale,
                maximum = sorted[sorted.Length - 1] * scale
            };
        }

        private static double Percentile(long[] sorted, double percentile)
        {
            if (sorted.Length == 1) return sorted[0];
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }
    }
}
