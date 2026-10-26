using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.Logging
{
    public sealed class LoggingDemo : MonoBehaviour
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

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(LoggingKind.Baseline);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(LoggingKind.DisabledEagerConcat);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(LoggingKind.DisabledGuardedConcat);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(LoggingKind.DisabledEagerFormat);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(LoggingKind.DisabledGuardedFormat);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1120f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Logging · Disabled output still has argument costs", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("The scenario disables Debug.unityLogger.logEnabled during every run:", _gui.Label);
            GUILayout.Label("[0] Baseline loop", _gui.Label);
            GUILayout.Label("[1] Disabled Debug.Log with eager string concatenation", _gui.Label);
            GUILayout.Label("[2] Call-site guard before string concatenation", _gui.Label);
            GUILayout.Label("[3] Disabled Debug.LogFormat with params arguments", _gui.Label);
            GUILayout.Label("[4] Call-site guard before LogFormat arguments", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: operations per sample", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Operations: " + _operationCounts[_operationCountIndex], _gui.Label);
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
            GUILayout.Space(8f);
            GUILayout.Label(
                "C# evaluates method arguments before Debug.Log or Debug.LogFormat can observe that logging is disabled. " +
                "Concatenation creates strings; the params overload also creates an object array and boxes value types. " +
                "A call-site guard skips argument construction. Actual enabled logging also includes Console, stack-trace and Editor overhead and is intentionally not spammed here.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(LoggingKind kind)
        {
            _exportStatus = string.Empty;
            int operations = _operationCounts[_operationCountIndex];
            _harness.Run(
                new LoggingScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 20,
                    sampleFrames = 120,
                    operationsPerStep = operations,
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
