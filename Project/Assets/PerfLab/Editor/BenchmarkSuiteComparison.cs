using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PerfLab.Benchmarking;
using UnityEditor;
using UnityEngine;

namespace PerfLab.Editor
{
    internal static class BenchmarkSuiteComparison
    {
        [MenuItem("PerfLab/Benchmarks/Compare Latest Suite Summaries")]
        private static void CompareLatest()
        {
            string directory = BenchmarkResultExporter.GetExportDirectory();
            string[] paths = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*-suite-summary.json")
                : new string[0];
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length < 2)
            {
                EditorUtility.DisplayDialog(
                    "Benchmark comparison",
                    "Run the automated suite at least twice before comparing results.",
                    "OK");
                return;
            }

            string baselinePath = paths[paths.Length - 2];
            string currentPath = paths[paths.Length - 1];
            SuiteSummary baseline = ReadSummary(baselinePath);
            SuiteSummary current = ReadSummary(currentPath);
            BenchmarkEnvironment baselineEnvironment = ReadEnvironment(baseline);
            BenchmarkEnvironment currentEnvironment = ReadEnvironment(current);
            string environmentMismatch = CompareEnvironment(
                baselineEnvironment,
                currentEnvironment);

            var comparisons = new List<ScenarioComparison>();
            var baselineByScenario = new Dictionary<string, SuiteEntry>();
            for (var i = 0; i < baseline.entries.Length; i++)
                baselineByScenario[baseline.entries[i].scenario] = baseline.entries[i];

            for (var i = 0; i < current.entries.Length; i++)
            {
                SuiteEntry currentEntry = current.entries[i];
                SuiteEntry baselineEntry;
                if (!baselineByScenario.TryGetValue(
                        currentEntry.scenario,
                        out baselineEntry))
                {
                    comparisons.Add(new ScenarioComparison
                    {
                        scenario = currentEntry.scenario,
                        comparable = false,
                        note = "Scenario is missing from the baseline"
                    });
                    continue;
                }

                comparisons.Add(new ScenarioComparison
                {
                    scenario = currentEntry.scenario,
                    comparable = string.IsNullOrEmpty(environmentMismatch),
                    baselineP50Milliseconds = baselineEntry.p50Milliseconds,
                    currentP50Milliseconds = currentEntry.p50Milliseconds,
                    p50ChangePercent = ChangePercent(
                        baselineEntry.p50Milliseconds,
                        currentEntry.p50Milliseconds),
                    baselineP95Milliseconds = baselineEntry.p95Milliseconds,
                    currentP95Milliseconds = currentEntry.p95Milliseconds,
                    p95ChangePercent = ChangePercent(
                        baselineEntry.p95Milliseconds,
                        currentEntry.p95Milliseconds),
                    baselineP99Milliseconds = baselineEntry.p99Milliseconds,
                    currentP99Milliseconds = currentEntry.p99Milliseconds,
                    p99ChangePercent = ChangePercent(
                        baselineEntry.p99Milliseconds,
                        currentEntry.p99Milliseconds),
                    note = environmentMismatch
                });
            }

            var report = new ComparisonReport
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                comparable = string.IsNullOrEmpty(environmentMismatch),
                environmentMismatch = environmentMismatch,
                baselineSummaryPath = baselinePath,
                currentSummaryPath = currentPath,
                scenarios = comparisons.ToArray()
            };
            string outputPath = Path.Combine(
                directory,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") +
                "-suite-comparison.json");
            File.WriteAllText(
                outputPath,
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            if (report.comparable)
            {
                Debug.Log("Benchmark suites compared: " + outputPath);
                EditorUtility.DisplayDialog(
                    "Benchmark comparison",
                    "Comparison written to:\n" + outputPath,
                    "OK");
            }
            else
            {
                Debug.LogWarning(
                    "Benchmark suites are not comparable: " +
                    environmentMismatch + "\n" + outputPath);
                EditorUtility.DisplayDialog(
                    "Benchmark comparison",
                    "Results were recorded, but must not be treated as a regression comparison:\n" +
                    environmentMismatch,
                    "OK");
            }
        }

        private static SuiteSummary ReadSummary(string path)
        {
            SuiteSummary result = JsonUtility.FromJson<SuiteSummary>(
                File.ReadAllText(path));
            if (result == null || result.entries == null)
                throw new InvalidDataException("Invalid suite summary: " + path);
            return result;
        }

        private static BenchmarkEnvironment ReadEnvironment(SuiteSummary summary)
        {
            if (summary.entries.Length == 0 ||
                string.IsNullOrEmpty(summary.entries[0].resultPath) ||
                !File.Exists(summary.entries[0].resultPath))
            {
                return null;
            }

            BenchmarkResult result = JsonUtility.FromJson<BenchmarkResult>(
                File.ReadAllText(summary.entries[0].resultPath));
            return result == null ? null : result.environment;
        }

        private static string CompareEnvironment(
            BenchmarkEnvironment baseline,
            BenchmarkEnvironment current)
        {
            if (baseline == null || current == null)
                return "Environment metadata is missing";
            if (baseline.unityVersion != current.unityVersion)
                return "Unity version differs";
            if (baseline.platform != current.platform)
                return "Platform differs";
            if (baseline.processor != current.processor)
                return "Processor differs";
            if (baseline.graphicsDevice != current.graphicsDevice ||
                baseline.graphicsApi != current.graphicsApi)
                return "Graphics device or API differs";
            if (baseline.width != current.width || baseline.height != current.height)
                return "Resolution differs";
            if (baseline.qualityLevel != current.qualityLevel)
                return "Quality level differs";
            return string.Empty;
        }

        private static double ChangePercent(double baseline, double current)
        {
            if (Math.Abs(baseline) < 0.000000001d) return 0d;
            return (current - baseline) / baseline * 100d;
        }

        [Serializable]
        private sealed class SuiteSummary
        {
            public SuiteEntry[] entries;
        }

        [Serializable]
        private sealed class SuiteEntry
        {
            public string scenario;
            public double p50Milliseconds;
            public double p95Milliseconds;
            public double p99Milliseconds;
            public string resultPath;
        }

        [Serializable]
        private sealed class ComparisonReport
        {
            public string utcTimestamp;
            public bool comparable;
            public string environmentMismatch;
            public string baselineSummaryPath;
            public string currentSummaryPath;
            public ScenarioComparison[] scenarios;
        }

        [Serializable]
        private sealed class ScenarioComparison
        {
            public string scenario;
            public bool comparable;
            public double baselineP50Milliseconds;
            public double currentP50Milliseconds;
            public double p50ChangePercent;
            public double baselineP95Milliseconds;
            public double currentP95Milliseconds;
            public double p95ChangePercent;
            public double baselineP99Milliseconds;
            public double currentP99Milliseconds;
            public double p99ChangePercent;
            public string note;
        }
    }
}
