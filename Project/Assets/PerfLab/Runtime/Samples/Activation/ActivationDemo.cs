using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.Activation
{
    public sealed class ActivationDemo : MonoBehaviour
    {
        private readonly int[] _nodeCounts = { 100, 1000, 5000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _nodeCountIndex = 1;
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
                _nodeCountIndex = Mathf.Max(0, _nodeCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _nodeCountIndex = Mathf.Min(_nodeCounts.Length - 1, _nodeCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(ActivationKind.DataFlags);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(ActivationKind.BehaviourEnabled);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(ActivationKind.IndividualSetActive);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(ActivationKind.ParentSetActive);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Activation · Flags, components, and hierarchies", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("Every mode changes each logical node off and on:", _gui.Label);
            GUILayout.Label("[0] Plain bool flags", _gui.Label);
            GUILayout.Label("[1] Disable/enable each MonoBehaviour", _gui.Label);
            GUILayout.Label("[2] SetActive(false/true) on each child GameObject", _gui.Label);
            GUILayout.Label("[3] SetActive(false/true) once on their common parent", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: node count", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Nodes: " + _nodeCounts[_nodeCountIndex], _gui.Label);
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
                "A bool only changes managed data. Behaviour.enabled also changes Unity callback state. SetActive traverses activation state and invokes OnEnable/OnDisable; real Renderers, Colliders and Animators would add subsystem registration work. " +
                "Parent toggling reduces API calls but still visits every affected descendant.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(ActivationKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new ActivationScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 20,
                    sampleFrames = 120,
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
