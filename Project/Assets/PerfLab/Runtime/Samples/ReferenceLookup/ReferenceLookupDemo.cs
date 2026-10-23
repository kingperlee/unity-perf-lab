using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.ReferenceLookup
{
    public sealed class ReferenceLookupDemo : MonoBehaviour
    {
        private readonly int[] _operationCounts = { 100, 1000, 10000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _operationCountIndex = 1;
        private string _exportStatus = string.Empty;
        private GameObject _demoCameraObject;
        private Vector2 _scrollPosition;

        private void Awake()
        {
            EnsureCamera();
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _operationCountIndex = Mathf.Max(0, _operationCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _operationCountIndex = Mathf.Min(_operationCounts.Length - 1, _operationCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(ReferenceLookupKind.CachedComponent);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(ReferenceLookupKind.GetComponent);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(ReferenceLookupKind.TryGetComponent);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(ReferenceLookupKind.CachedChildTransform);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(ReferenceLookupKind.TransformFindDirectChild);
            if (Input.GetKeyDown(KeyCode.Alpha5)) Run(ReferenceLookupKind.TransformFindPath);
            if (Input.GetKeyDown(KeyCode.Alpha6)) Run(ReferenceLookupKind.GameObjectFind);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Reference lookup · Cache versus search", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("Component table on the same GameObject:", _gui.Label);
            GUILayout.Label("[0] Cached component   [1] GetComponent   [2] TryGetComponent", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Local hierarchy:", _gui.Label);
            GUILayout.Label("[3] Cached Transform   [4] Find direct child among 64", _gui.Label);
            GUILayout.Label("[5] Transform.Find through an 8-level path", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Whole active scene:", _gui.Label);
            GUILayout.Label("[6] GameObject.Find among 1,000 distractors", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: operations per sample", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Operations: " + _operationCounts[_operationCountIndex], _gui.Label);
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
                "Compare 0–2 with each other and 3–5 with each other. GameObject.Find has different global-search semantics and is shown as a hot-path warning, not as a drop-in replacement for GetComponent.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(ReferenceLookupKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new ReferenceLookupScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 20,
                    sampleFrames = 120,
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
