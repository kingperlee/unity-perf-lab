using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.DataLayout
{
    public sealed class DataLayoutDemo : MonoBehaviour
    {
        private readonly int[] _elementCounts = { 10000, 100000, 1000000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _elementCountIndex = 1;
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

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(DataLayoutKind.CompactAosAllFields);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(DataLayoutKind.SoaAllFields);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(DataLayoutKind.CompactAosHotFields);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(DataLayoutKind.WideAosHotFields);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(DataLayoutKind.SoaHotFields);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(620f), _gui.Box);
            GUILayout.Label("Data layout · AoS vs SoA", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("All six hot fields (same arithmetic):", _gui.Label);
            GUILayout.Label("[0] Compact AoS (24-byte struct)   [1] SoA", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Only position X / velocity X (same arithmetic):", _gui.Label);
            GUILayout.Label("[2] Compact AoS   [3] Wide AoS (64-byte struct)", _gui.Label);
            GUILayout.Label("[4] Hot/cold split SoA", _gui.Label);
            GUILayout.Label("Left bracket: fewer elements   Right bracket: more elements", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Label("Elements: " + _elementCounts[_elementCountIndex] + "   Passes/sample: 4", _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);

            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Step P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms", _gui.Label);
            }

            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.EndArea();
        }

        private void Run(DataLayoutKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new DataLayoutScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 30,
                    sampleFrames = 180,
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
