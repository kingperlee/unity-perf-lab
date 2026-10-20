using System.Collections.Generic;

namespace PerfLab.Benchmarking
{
    public static class BenchmarkBudgetEvaluator
    {
        public static BenchmarkBudgetEvaluation Evaluate(
            BenchmarkBudget budget,
            BenchmarkDistribution elapsedMilliseconds,
            BenchmarkDistribution allocatedBytes)
        {
            var failures = new List<string>(4);
            if (budget.maxP50Milliseconds >= 0d &&
                elapsedMilliseconds.p50 > budget.maxP50Milliseconds)
            {
                failures.Add(FormatFailure(
                    "elapsed P50",
                    elapsedMilliseconds.p50,
                    budget.maxP50Milliseconds,
                    "ms"));
            }

            if (budget.maxP95Milliseconds >= 0d &&
                elapsedMilliseconds.p95 > budget.maxP95Milliseconds)
            {
                failures.Add(FormatFailure(
                    "elapsed P95",
                    elapsedMilliseconds.p95,
                    budget.maxP95Milliseconds,
                    "ms"));
            }

            if (budget.maxP99Milliseconds >= 0d &&
                elapsedMilliseconds.p99 > budget.maxP99Milliseconds)
            {
                failures.Add(FormatFailure(
                    "elapsed P99",
                    elapsedMilliseconds.p99,
                    budget.maxP99Milliseconds,
                    "ms"));
            }

            if (budget.maxP95AllocatedBytes >= 0d &&
                allocatedBytes.p95 > budget.maxP95AllocatedBytes)
            {
                failures.Add(FormatFailure(
                    "allocation P95",
                    allocatedBytes.p95,
                    budget.maxP95AllocatedBytes,
                    "bytes"));
            }

            return new BenchmarkBudgetEvaluation
            {
                configured = budget.IsConfigured,
                passed = !budget.IsConfigured || failures.Count == 0,
                failures = failures.ToArray()
            };
        }

        private static string FormatFailure(
            string metric,
            double actual,
            double maximum,
            string unit)
        {
            return string.Format(
                "{0}: {1:F4} {2} > budget {3:F4} {2}",
                metric,
                actual,
                unit,
                maximum);
        }
    }
}
