using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PerfLab.Benchmarking;
using PerfLab.Samples.GcAllocations;
using PerfLab.Samples.JobsBurst;
using PerfLab.Samples.PhysicsQueries;
using PerfLab.Samples.TextureUploads;
using PerfLab.Samples.UpdateDispatch;
using UnityEngine;

namespace PerfLab.Samples.Automation
{
    public sealed class AutomatedBenchmarkSuite : MonoBehaviour
    {
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private readonly List<SuiteEntry> _entries = new List<SuiteEntry>();
        private List<SuiteDefinition> _definitions;
        private BenchmarkHarness _harness;
        private int _currentIndex = -1;
        private bool _running;
        private string _status = "Press Space to run the suite";
        private string _summaryPath = string.Empty;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _harness = gameObject.AddComponent<BenchmarkHarness>();
            _definitions = CreateDefinitions();

            if (!Application.isEditor || HasCommandLineFlag("-benchmarkAutoRun"))
                StartSuite();
        }

        private void Update()
        {
            if (!_running)
            {
                if (Input.GetKeyDown(KeyCode.Space)) StartSuite();
                return;
            }

            if (_harness.State == BenchmarkState.Completed)
            {
                RecordCompletedResult();
                StartNext();
            }
            else if (_harness.State == BenchmarkState.Failed)
            {
                RecordFailure(_harness.LastFailure);
                StartNext();
            }
        }

        private void OnGUI()
        {
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(580f), _gui.Box);
            GUILayout.Label("Automated benchmark suite", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label(_status, _gui.Label);
            GUILayout.Label($"Progress: {_entries.Count} / {_definitions.Count}", _gui.Label);
            if (_running && _currentIndex >= 0 && _currentIndex < _definitions.Count)
                GUILayout.Label("Current: " + _definitions[_currentIndex].name, _gui.Label);
            if (!string.IsNullOrEmpty(_summaryPath))
                GUILayout.Label("Summary: " + _summaryPath, _gui.Label);
            GUILayout.EndArea();
        }

        private void StartSuite()
        {
            if (_running) return;
            _entries.Clear();
            _summaryPath = string.Empty;
            _currentIndex = -1;
            _running = true;
            StartNext();
        }

        private void StartNext()
        {
            _currentIndex++;
            if (_currentIndex >= _definitions.Count)
            {
                CompleteSuite();
                return;
            }

            SuiteDefinition definition = _definitions[_currentIndex];
            _status = "Running " + definition.name;
            _harness.Run(definition.createScenario(), definition.configuration);
        }

        private void RecordCompletedResult()
        {
            BenchmarkResult result = _harness.LastResult;
            string resultPath = BenchmarkResultExporter.Export(result);
            _entries.Add(new SuiteEntry
            {
                scenario = result.scenario,
                succeeded = true,
                budgetConfigured = result.budgetEvaluation.configured,
                budgetPassed = result.budgetEvaluation.passed,
                p50Milliseconds = result.elapsedMilliseconds.p50,
                p95Milliseconds = result.elapsedMilliseconds.p95,
                p99Milliseconds = result.elapsedMilliseconds.p99,
                resultPath = resultPath,
                error = string.Empty
            });
        }

        private void RecordFailure(Exception exception)
        {
            _entries.Add(new SuiteEntry
            {
                scenario = _definitions[_currentIndex].name,
                succeeded = false,
                budgetPassed = false,
                resultPath = string.Empty,
                error = exception == null ? "Unknown benchmark failure" : exception.ToString()
            });
        }

        private void CompleteSuite()
        {
            _running = false;
            bool passed = true;
            for (var i = 0; i < _entries.Count; i++)
            {
                SuiteEntry entry = _entries[i];
                if (!entry.succeeded || (entry.budgetConfigured && !entry.budgetPassed))
                    passed = false;
            }

            var report = new SuiteReport
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                passed = passed,
                entries = _entries.ToArray()
            };
            string directory = BenchmarkResultExporter.GetExportDirectory();
            Directory.CreateDirectory(directory);
            _summaryPath = Path.Combine(
                directory,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-suite-summary.json");
            File.WriteAllText(
                _summaryPath,
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
            _status = passed ? "Suite completed: PASS" : "Suite completed: FAIL";
        }

        private static List<SuiteDefinition> CreateDefinitions()
        {
            return new List<SuiteDefinition>
            {
                Define("Update/MonoBehaviour", () => new NativeUpdateScenario(), 5000, 60, 120),
                Define("Update/Central", () => new CentralUpdateScenario(), 5000, 60, 120),
                Define("GC/Baseline", () => new BaselineLoopScenario(), 1000, 30, 120),
                Define("GC/Boxing", () => new BoxingScenario(), 1000, 30, 120),
                Define("Physics/RaycastAll", () => new PhysicsQueryScenario(PhysicsQueryKind.RaycastAll, false), 500, 30, 120),
                Define("Physics/RaycastNonAlloc", () => new PhysicsQueryScenario(PhysicsQueryKind.RaycastNonAlloc, false), 500, 30, 120),
                Define("Jobs/MainThread", () => new JobsBurstScenario(JobsBurstKind.MainThread), 100000, 30, 120),
                Define("Jobs/BurstParallel", () => new JobsBurstScenario(JobsBurstKind.BurstParallelJob), 100000, 30, 120),
                Define("Texture/NoMipmaps", () => new TextureUploadScenario(TextureUploadKind.UploadWithoutMipmaps), 1024, 20, 60),
                Define("Texture/WithMipmaps", () => new TextureUploadScenario(TextureUploadKind.UploadWithMipmaps), 1024, 20, 60)
            };
        }

        private static SuiteDefinition Define(
            string name,
            Func<IBenchmarkScenario> createScenario,
            int operations,
            int warmupFrames,
            int sampleFrames)
        {
            return new SuiteDefinition
            {
                name = name,
                createScenario = createScenario,
                configuration = new BenchmarkConfiguration
                {
                    operationsPerStep = operations,
                    warmupFrames = warmupFrames,
                    sampleFrames = sampleFrames,
                    randomSeed = 12345
                }
            };
        }

        private static bool HasCommandLineFlag(string flag)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private sealed class SuiteDefinition
        {
            public string name;
            public Func<IBenchmarkScenario> createScenario;
            public BenchmarkConfiguration configuration;
        }

        [Serializable]
        private sealed class SuiteReport
        {
            public string utcTimestamp;
            public bool passed;
            public SuiteEntry[] entries;
        }

        [Serializable]
        private sealed class SuiteEntry
        {
            public string scenario;
            public bool succeeded;
            public bool budgetConfigured;
            public bool budgetPassed;
            public double p50Milliseconds;
            public double p95Milliseconds;
            public double p99Milliseconds;
            public string resultPath;
            public string error;
        }
    }
}
