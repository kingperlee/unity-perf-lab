using System.Threading;
using UnityEngine;

namespace PerfLab.Samples.FixedTimestep
{
    public sealed class FixedTimestepDemo : MonoBehaviour
    {
        private readonly float[] _fixedTimesteps = { 0.02f, 0.01f, 0.005f };
        private readonly float[] _maximumDeltaTimes = { 1f / 3f, 0.05f };
        private readonly int[] _workIterations = { 0, 10000, 50000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();

        private float _originalFixedDeltaTime;
        private float _originalMaximumDeltaTime;
        private int _originalVSyncCount;
        private int _originalTargetFrameRate;
        private int _fixedTimestepIndex;
        private int _maximumDeltaTimeIndex;
        private int _workIndex;
        private int _fixedCallsSinceUpdate;
        private int _fixedCallsLastFrame;
        private int _maxFixedCallsInFrame;
        private int _lastInjectedStallMs;
        private float _lastFrameMilliseconds;
        private float _workSink;

        private void Awake()
        {
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            _originalMaximumDeltaTime = Time.maximumDeltaTime;
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            ApplyTimingSettings();
        }

        private void FixedUpdate()
        {
            _fixedCallsSinceUpdate++;

            float value = _workSink + 0.001f;
            int iterations = _workIterations[_workIndex];
            for (int i = 0; i < iterations; i++)
                value = value * 1.000001f + (i & 7) * 0.000001f;
            _workSink = value;
        }

        private void Update()
        {
            _fixedCallsLastFrame = _fixedCallsSinceUpdate;
            _fixedCallsSinceUpdate = 0;
            _maxFixedCallsInFrame = Mathf.Max(_maxFixedCallsInFrame, _fixedCallsLastFrame);
            _lastFrameMilliseconds = Time.unscaledDeltaTime * 1000f;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                _fixedTimestepIndex = Mathf.Max(0, _fixedTimestepIndex - 1);
                ApplyTimingSettings();
                ResetPeak();
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                _fixedTimestepIndex = Mathf.Min(_fixedTimesteps.Length - 1, _fixedTimestepIndex + 1);
                ApplyTimingSettings();
                ResetPeak();
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                _maximumDeltaTimeIndex = (_maximumDeltaTimeIndex + 1) % _maximumDeltaTimes.Length;
                ApplyTimingSettings();
                ResetPeak();
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                _workIndex = (_workIndex + 1) % _workIterations.Length;
                ResetPeak();
            }
            if (Input.GetKeyDown(KeyCode.R)) ResetPeak();

            if (Input.GetKeyDown(KeyCode.Alpha1)) InjectStall(50);
            if (Input.GetKeyDown(KeyCode.Alpha2)) InjectStall(100);
            if (Input.GetKeyDown(KeyCode.Alpha3)) InjectStall(250);
        }

        private void OnGUI()
        {
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(650f), _gui.Box);
            GUILayout.Label("Fixed timestep · Catch-up behavior", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("Left bracket / Right bracket: change fixed timestep", _gui.Label);
            GUILayout.Label("[M] Toggle maximumDeltaTime   [W] FixedUpdate workload", _gui.Label);
            GUILayout.Label("[1] Stall 50 ms   [2] Stall 100 ms   [3] Stall 250 ms", _gui.Label);
            GUILayout.Label("[R] Reset observed peak", _gui.Label);
            GUILayout.Space(8f);

            float fixedStep = _fixedTimesteps[_fixedTimestepIndex];
            GUILayout.Label($"Fixed timestep: {fixedStep * 1000f:F1} ms ({1f / fixedStep:F0} Hz)", _gui.Label);
            GUILayout.Label($"maximumDeltaTime: {_maximumDeltaTimes[_maximumDeltaTimeIndex] * 1000f:F1} ms", _gui.Label);
            GUILayout.Label($"Work per FixedUpdate: {_workIterations[_workIndex]:N0} iterations", _gui.Label);
            GUILayout.Label($"Last rendered-frame interval: {_lastFrameMilliseconds:F2} ms", _gui.Label);
            GUILayout.Label($"FixedUpdate calls before this Update: {_fixedCallsLastFrame}", _gui.Label);
            GUILayout.Label($"Maximum observed in one rendered frame: {_maxFixedCallsInFrame}", _gui.Label);
            GUILayout.Label($"Last injected main-thread stall: {_lastInjectedStallMs} ms", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "A stall accumulates simulation time. On the next rendered frame Unity may run FixedUpdate repeatedly to catch up. " +
                "A smaller fixed timestep requests more physics steps; maximumDeltaTime limits how much catch-up Unity accepts in one frame.",
                _gui.Label);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            Time.maximumDeltaTime = _originalMaximumDeltaTime;
            QualitySettings.vSyncCount = _originalVSyncCount;
            Application.targetFrameRate = _originalTargetFrameRate;
        }

        private void ApplyTimingSettings()
        {
            Time.fixedDeltaTime = _fixedTimesteps[_fixedTimestepIndex];
            Time.maximumDeltaTime = _maximumDeltaTimes[_maximumDeltaTimeIndex];
        }

        private void InjectStall(int milliseconds)
        {
            _lastInjectedStallMs = milliseconds;
            Thread.Sleep(milliseconds);
        }

        private void ResetPeak()
        {
            _maxFixedCallsInFrame = 0;
        }
    }
}
