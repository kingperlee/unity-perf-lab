using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.UpdateDispatch
{
    public sealed class UpdateDispatchDemo : MonoBehaviour
    {
        private readonly int[] _agentCounts = { 1000, 5000, 10000, 20000 };
        private BenchmarkHarness _harness;
        private int _agentCountIndex = 1;
        private string _exportStatus = string.Empty;
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                _agentCountIndex = Mathf.Max(0, _agentCountIndex - 1);
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                _agentCountIndex = Mathf.Min(_agentCounts.Length - 1, _agentCountIndex + 1);
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(new NativeUpdateScenario());
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(new CentralUpdateScenario());
            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
            {
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
            }
        }

        private void OnGUI()
        {
            // IMGUI allocates. Suppress it for the entire warm-up/sample window so
            // its work does not become part of the frame-interval comparison.
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(390f), _gui.Box);
            GUILayout.Label("Update Dispatch · Frame-interval benchmark", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[1] MonoBehaviour.Update   [2] Central manager", _gui.Label);
            GUILayout.Label("[ / ] Agent count   [E] Export last result", _gui.Label);
            GUILayout.Label("Agents: " + _agentCounts[_agentCountIndex], _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);
            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                var allocation = _harness.LastResult.allocatedBytes;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Frame P50/P95/P99: {timing.p50:F3} / {timing.p95:F3} / {timing.p99:F3} ms", _gui.Label);
                GUILayout.Label($"GC/frame P50/P95/P99: {allocation.p50:F0} / {allocation.p95:F0} / {allocation.p99:F0} bytes", _gui.Label);
            }
            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.EndArea();
        }

        private void Run(IBenchmarkScenario scenario)
        {
            _exportStatus = string.Empty;
            _harness.Run(scenario, new BenchmarkConfiguration
            {
                warmupFrames = 120,
                sampleFrames = 300,
                operationsPerStep = _agentCounts[_agentCountIndex],
                randomSeed = 12345
            });
        }

        private bool IsRunning()
        {
            return _harness != null &&
                   (_harness.State == BenchmarkState.SettingUp ||
                    _harness.State == BenchmarkState.WarmingUp ||
                    _harness.State == BenchmarkState.Sampling ||
                    _harness.State == BenchmarkState.Finalizing);
        }
    }
}
