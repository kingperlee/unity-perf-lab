using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.PropertyIds
{
    public sealed class PropertyIdDemo : MonoBehaviour
    {
        private readonly int[] _operationCounts = { 1000, 10000, 100000 };
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

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(PropertyIdKind.MaterialSetColorString);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(PropertyIdKind.MaterialSetColorCachedId);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(PropertyIdKind.ShaderPropertyToId);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(PropertyIdKind.CachedShaderPropertyId);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(PropertyIdKind.AnimatorStringToHash);
            if (Input.GetKeyDown(KeyCode.Alpha5)) Run(PropertyIdKind.CachedAnimatorHash);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("String identifiers · Resolve once, reuse integers", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("Real Material property calls:", _gui.Label);
            GUILayout.Label("[0] Material.SetColor(string)   [1] SetColor(cached ID)", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Shader property ID resolution:", _gui.Label);
            GUILayout.Label("[2] Shader.PropertyToID every time   [3] Cached ID", _gui.Label);
            GUILayout.Space(5f);
            GUILayout.Label("Animator parameter hash resolution:", _gui.Label);
            GUILayout.Label("[4] Animator.StringToHash every time   [5] Cached hash", _gui.Label);
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
                "PropertyToID and StringToHash return stable integer identifiers for the current runtime. Cache them in static readonly fields for hot paths. " +
                "The string overload is convenient for initialization and tools; it does not create a new managed string when the literal is reused, but it still repeats name resolution and native-call work.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(PropertyIdKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new PropertyIdScenario(kind),
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
