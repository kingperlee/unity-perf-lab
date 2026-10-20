using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.TransformHierarchy
{
    public sealed class TransformHierarchyDemo : MonoBehaviour
    {
        private readonly int[] _nodeCounts = { 1000, 5000, 10000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _nodeCountIndex = 1;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _nodeCountIndex = Mathf.Max(0, _nodeCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _nodeCountIndex = Mathf.Min(_nodeCounts.Length - 1, _nodeCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(TransformHierarchyKind.FlatRootMove);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(TransformHierarchyKind.Depth16RootMove);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(TransformHierarchyKind.FlatLeafMove);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(TransformHierarchyKind.ReparentLeaves);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(610f), _gui.Box);
            GUILayout.Label("Transform hierarchy · Dirty propagation", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("Root motion + read every world position:", _gui.Label);
            GUILayout.Label("[0] Flat leaves   [1] Depth-16 chains", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Flat leaves + read every world position:", _gui.Label);
            GUILayout.Label("[2] Move every leaf   [3] Reparent every leaf", _gui.Label);
            GUILayout.Label("Left bracket: fewer nodes   Right bracket: more nodes", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Label("Nodes: " + _nodeCounts[_nodeCountIndex], _gui.Label);
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

        private void Run(TransformHierarchyKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new TransformHierarchyScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 30,
                    sampleFrames = 180,
                    operationsPerStep = _nodeCounts[_nodeCountIndex],
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
