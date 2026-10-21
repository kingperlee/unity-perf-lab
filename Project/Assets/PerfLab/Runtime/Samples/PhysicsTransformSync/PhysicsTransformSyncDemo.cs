using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.PhysicsTransformSync
{
    public sealed class PhysicsTransformSyncDemo : MonoBehaviour
    {
        private readonly int[] _colliderCounts = { 100, 500, 1000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private GameObject _demoCameraObject;
        private Vector2 _scrollPosition;
        private int _colliderCountIndex = 1;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            EnsureCamera();
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _colliderCountIndex = Mathf.Max(0, _colliderCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _colliderCountIndex = Mathf.Min(_colliderCounts.Length - 1, _colliderCountIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(PhysicsTransformSyncKind.BatchExplicitSync);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(PhysicsTransformSyncKind.InterleavedExplicitSync);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(PhysicsTransformSyncKind.InterleavedAutoSync);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Physics transform synchronization", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("All modes perform the same moves and raycasts:", _gui.Label);
            GUILayout.Label("[0] Move all, explicit sync once, then query all", _gui.Label);
            GUILayout.Label("[1] Move, explicit sync, query — for every collider", _gui.Label);
            GUILayout.Label("[2] Move and query interleaved with autoSyncTransforms", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: collider count", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Colliders: " + _colliderCounts[_colliderCountIndex], _gui.Label);
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
                "Batching Transform writes before one synchronization avoids repeatedly updating the physics broadphase. " +
                "Interleaving a query means each query needs current collider state, so Unity loses that batching opportunity.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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

        private void Run(PhysicsTransformSyncKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new PhysicsTransformSyncScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 20,
                    sampleFrames = 120,
                    operationsPerStep = _colliderCounts[_colliderCountIndex],
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
