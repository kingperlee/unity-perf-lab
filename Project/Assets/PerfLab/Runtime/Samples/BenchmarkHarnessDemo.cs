using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples
{
    public sealed class BenchmarkHarnessDemo : MonoBehaviour
    {
        private BenchmarkHarness _harness;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !IsRunning())
            {
                _exportStatus = string.Empty;
                _harness.Run(new ArithmeticBenchmarkScenario(), new BenchmarkConfiguration());
            }

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
            {
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, 720f, 240f), GUI.skin.box);
            GUILayout.Label("Unity Performance Lab · Benchmark Harness");
            GUILayout.Label("[Space] Run calibration   [E] Export completed result");
            GUILayout.Label("State: " + _harness.State + "   Samples: " + _harness.CompletedSamples);
            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                var allocation = _harness.LastResult.allocatedBytes;
                GUILayout.Label($"Work P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms");
                GUILayout.Label($"Allocated P50/P95/P99: {allocation.p50:F0} / {allocation.p95:F0} / {allocation.p99:F0} bytes");
            }
            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus);
            GUILayout.EndArea();
        }

        private bool IsRunning()
        {
            return _harness.State == BenchmarkState.SettingUp ||
                   _harness.State == BenchmarkState.WarmingUp ||
                   _harness.State == BenchmarkState.Sampling ||
                   _harness.State == BenchmarkState.Finalizing;
        }
    }
}
