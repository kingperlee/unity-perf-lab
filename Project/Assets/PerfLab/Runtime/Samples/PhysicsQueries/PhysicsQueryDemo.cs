using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.PhysicsQueries
{
    public sealed class PhysicsQueryDemo : MonoBehaviour
    {
        private readonly int[] _queryCounts = { 100, 500, 1000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _queryCountIndex = 0;
        private bool _useTargetMask;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _queryCountIndex = Mathf.Max(0, _queryCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _queryCountIndex = Mathf.Min(_queryCounts.Length - 1, _queryCountIndex + 1);
            if (Input.GetKeyDown(KeyCode.M)) _useTargetMask = !_useTargetMask;

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(PhysicsQueryKind.RaycastSingle);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(PhysicsQueryKind.RaycastAll);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(PhysicsQueryKind.RaycastNonAlloc);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(PhysicsQueryKind.OverlapSphere);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(PhysicsQueryKind.OverlapSphereNonAlloc);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(520f), _gui.Box);
            GUILayout.Label("Physics Queries · Measured-step benchmark", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Raycast single", _gui.Label);
            GUILayout.Label("[1] RaycastAll   [2] RaycastNonAlloc", _gui.Label);
            GUILayout.Label("[3] OverlapSphere   [4] OverlapSphereNonAlloc", _gui.Label);
            GUILayout.Label("Left bracket: fewer queries   Right bracket: more queries", _gui.Label);
            GUILayout.Label("[M] Toggle LayerMask   [E] Export last result", _gui.Label);
            GUILayout.Label("Queries/sample: " + _queryCounts[_queryCountIndex] +
                            "   Mask: " + (_useTargetMask ? "Target layer only" : "All layers"), _gui.Label);
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

        private void Run(PhysicsQueryKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new PhysicsQueryScenario(kind, _useTargetMask),
                new BenchmarkConfiguration
                {
                    warmupFrames = 30,
                    sampleFrames = 300,
                    operationsPerStep = _queryCounts[_queryCountIndex],
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
