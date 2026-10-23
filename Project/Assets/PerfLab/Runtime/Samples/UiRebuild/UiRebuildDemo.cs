using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.UiRebuild
{
    public sealed class UiRebuildDemo : MonoBehaviour
    {
        private readonly int[] _elementCounts = { 100, 500, 1000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _elementCountIndex = 1;
        private string _exportStatus = string.Empty;
        private Vector2 _scrollPosition;
        private GameObject _demoCameraObject;

        private void Awake()
        {
            EnsureCamera();
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _elementCountIndex = Mathf.Max(0, _elementCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _elementCountIndex = Mathf.Min(_elementCounts.Length - 1, _elementCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(UiRebuildKind.DataOnly);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(UiRebuildKind.GraphicColorAndCanvasUpdate);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(UiRebuildKind.FullLayoutRebuild);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(UiRebuildKind.IsolatedLayoutRebuild);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("UGUI rebuild scope · Dirty work is deferred", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Change ordinary data only", _gui.Label);
            GUILayout.Label("[1] Change one Image color + force Canvas update", _gui.Label);
            GUILayout.Label("[2] Change one preferred height + rebuild full layout", _gui.Label);
            GUILayout.Label("[3] Same layout change inside an isolated 50-item Canvas", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: total UI elements", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Total UI elements: " + _elementCounts[_elementCountIndex], _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);
            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Step P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms", _gui.Label);
            }
            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Property assignment often only marks UI dirty; the expensive layout and mesh work normally occurs later. " +
                "This experiment forces completion inside the measured step. Separate Canvases can limit graphic rebuilds, while separate layout roots limit layout propagation.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(UiRebuildKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new UiRebuildScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 20,
                    sampleFrames = 120,
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

        private void EnsureCamera()
        {
            if (Camera.main != null) return;

            _demoCameraObject = new GameObject("Demo Camera");
            _demoCameraObject.tag = "MainCamera";
            var demoCamera = _demoCameraObject.AddComponent<Camera>();
            demoCamera.clearFlags = CameraClearFlags.SolidColor;
            demoCamera.backgroundColor = Color.black;
            demoCamera.cullingMask = 0;
        }

        private void OnDestroy()
        {
            if (_demoCameraObject != null) Destroy(_demoCameraObject);
        }
    }
}
