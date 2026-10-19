using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.GcAllocations
{
    public sealed class GcAllocationDemo : MonoBehaviour
    {
        private readonly int[] _operationCounts = { 100, 1000, 10000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _operationCountIndex = 1;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _operationCountIndex = Mathf.Max(0, _operationCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _operationCountIndex = Mathf.Min(_operationCounts.Length - 1, _operationCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(new BaselineLoopScenario());
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(new BoxingScenario());
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(new CapturingClosureScenario());
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(new LinqPipelineScenario());
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(new StringConstructionScenario());

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(470f), _gui.Box);
            GUILayout.Label("Managed Allocation · Measured-step benchmark", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Baseline loop   [1] Boxing   [2] Capturing closure", _gui.Label);
            GUILayout.Label("[3] LINQ pipeline   [4] String construction", _gui.Label);
            GUILayout.Label("[ / ] Operations per sample   [E] Export last result", _gui.Label);
            GUILayout.Label("Operations: " + _operationCounts[_operationCountIndex], _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);

            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                var allocation = _harness.LastResult.allocatedBytes;
                var rawAllocation = _harness.LastResult.rawAllocatedBytes;
                var baselineAllocation = _harness.LastResult.allocationBaselineBytes;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Step P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms", _gui.Label);
                GUILayout.Label($"Net allocation P50/P95/P99: {allocation.p50:F0} / {allocation.p95:F0} / {allocation.p99:F0} bytes", _gui.Label);
                GUILayout.Label($"Raw / baseline P50: {rawAllocation.p50:F0} / {baselineAllocation.p50:F0} bytes", _gui.Label);
                GUILayout.Label("Allocation source: " + _harness.LastResult.allocationMeasurement, _gui.Label);
                GUILayout.Label("Allocation probe: before " + _harness.LastResult.allocationProbeBefore +
                                ", after " + _harness.LastResult.allocationProbeAfter +
                                ", delta " + _harness.LastResult.allocationProbeDelta + " bytes", _gui.Label);
            }

            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.EndArea();
        }

        private void Run(IBenchmarkScenario scenario)
        {
            _exportStatus = string.Empty;
            _harness.Run(scenario, new BenchmarkConfiguration
            {
                warmupFrames = 30,
                sampleFrames = 300,
                operationsPerStep = _operationCounts[_operationCountIndex],
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
