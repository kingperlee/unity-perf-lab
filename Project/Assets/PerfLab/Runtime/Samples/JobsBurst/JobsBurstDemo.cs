using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.JobsBurst
{
    public sealed class JobsBurstDemo : MonoBehaviour
    {
        private readonly int[] _elementCounts = { 1000, 10000, 100000, 1000000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _elementCountIndex;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _elementCountIndex = Mathf.Max(0, _elementCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _elementCountIndex = Mathf.Min(_elementCounts.Length - 1, _elementCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(JobsBurstKind.MainThread);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(JobsBurstKind.SingleJob);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(JobsBurstKind.BurstSingleJob);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(JobsBurstKind.ParallelJob);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(JobsBurstKind.BurstParallelJob);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(570f), _gui.Box);
            GUILayout.Label("Jobs/Burst · Scheduling crossover", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Main thread   [1] Single IJob", _gui.Label);
            GUILayout.Label("[2] Burst IJob", _gui.Label);
            GUILayout.Label("[3] IJobParallelFor   [4] Burst IJobParallelFor", _gui.Label);
            GUILayout.Label("Left bracket: fewer elements   Right bracket: more elements", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Label("Elements/sample: " + _elementCounts[_elementCountIndex], _gui.Label);
            GUILayout.Label("Timing includes Schedule + Complete", _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);

            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                var allocation = _harness.LastResult.allocatedBytes;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Step P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms", _gui.Label);
                GUILayout.Label($"Net allocation P50/P95/P99: {allocation.p50:F0} / {allocation.p95:F0} / {allocation.p99:F0} bytes", _gui.Label);
            }

            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.EndArea();
        }

        private void Run(JobsBurstKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new JobsBurstScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 60,
                    sampleFrames = 300,
                    operationsPerStep = _elementCounts[_elementCountIndex],
                    randomSeed = 12345
                });
        }

        private bool IsRunning()
        {
            return _harness != null &&
                   (_harness.State == BenchmarkState.Calibrating ||
                    _harness.State == BenchmarkState.SettingUp ||
                    _harness.State == BenchmarkState.WarmingUp ||
                    _harness.State == BenchmarkState.Sampling ||
                    _harness.State == BenchmarkState.Finalizing);
        }
    }
}
