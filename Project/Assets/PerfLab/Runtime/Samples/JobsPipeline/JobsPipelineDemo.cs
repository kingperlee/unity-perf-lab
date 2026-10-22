using System.Diagnostics;
using PerfLab.Samples.JobsBurst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PerfLab.Samples.JobsPipeline
{
    public sealed class JobsPipelineDemo : MonoBehaviour
    {
        private enum ExecutionMode
        {
            Stopped,
            ImmediateComplete,
            DeferredCompletion
        }

        private readonly int[] _elementCounts = { 100000, 500000, 1000000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();

        private NativeArray<float> _values;
        private JobHandle _handle;
        private bool _jobOutstanding;
        private ExecutionMode _mode;
        private int _elementCountIndex;
        private int _scheduledFrame;
        private long _scheduledTimestamp;
        private int _completedJobs;
        private int _lastLatencyFrames;
        private float _maxFrameIntervalMilliseconds;
        private double _lastScheduleMilliseconds;
        private double _lastCompleteCallMilliseconds;
        private double _lastJobWallMilliseconds;
        private float _metricsStartedAt;
        private float _sink;
        private bool _ignoreNextFrameInterval;
        private GameObject _demoCameraObject;
        private Vector2 _scrollPosition;

        private void Awake()
        {
            EnsureCamera();
            AllocateValues();
            WarmUpJob();
            ResetMetrics();
        }

        private void Update()
        {
            if (_ignoreNextFrameInterval)
                _ignoreNextFrameInterval = false;
            else
                _maxFrameIntervalMilliseconds = Mathf.Max(
                    _maxFrameIntervalMilliseconds,
                    Time.unscaledDeltaTime * 1000f);

            if (Input.GetKeyDown(KeyCode.Alpha0)) SetMode(ExecutionMode.Stopped);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(ExecutionMode.ImmediateComplete);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(ExecutionMode.DeferredCompletion);
            if (Input.GetKeyDown(KeyCode.R)) ResetMetrics();

            if (Input.GetKeyDown(KeyCode.LeftBracket) && _elementCountIndex > 0)
            {
                _elementCountIndex--;
                ReallocateValues();
            }
            if (Input.GetKeyDown(KeyCode.RightBracket) && _elementCountIndex < _elementCounts.Length - 1)
            {
                _elementCountIndex++;
                ReallocateValues();
            }

            switch (_mode)
            {
                case ExecutionMode.ImmediateComplete:
                    ScheduleJob();
                    CompleteJob();
                    break;

                case ExecutionMode.DeferredCompletion:
                    if (_jobOutstanding && _handle.IsCompleted) CompleteJob();
                    if (!_jobOutstanding) ScheduleJob();
                    break;
            }
        }

        private void OnGUI()
        {
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Jobs pipeline · Wait now or publish later", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Stop   [1] Schedule + immediate Complete", _gui.Label);
            GUILayout.Label("[2] Schedule, poll IsCompleted, then publish", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: element count", _gui.Label);
            GUILayout.Label("[R] Reset observations", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Mode: " + _mode, _gui.Label);
            GUILayout.Label($"Elements per job: {_elementCounts[_elementCountIndex]:N0}", _gui.Label);
            GUILayout.Label("Outstanding job: " + (_jobOutstanding ? "Yes" : "No"), _gui.Label);
            GUILayout.Label($"Completed jobs: {_completedJobs:N0}", _gui.Label);
            GUILayout.Label($"Throughput: {CompletedJobsPerSecond():F2} jobs/s", _gui.Label);
            GUILayout.Label($"Last result latency: {_lastLatencyFrames} rendered frame(s)", _gui.Label);
            GUILayout.Label($"Last Schedule call: {_lastScheduleMilliseconds:F4} ms", _gui.Label);
            GUILayout.Label($"Last Complete call: {_lastCompleteCallMilliseconds:F4} ms", _gui.Label);
            GUILayout.Label($"Last schedule-to-result wall time: {_lastJobWallMilliseconds:F3} ms", _gui.Label);
            GUILayout.Label($"Largest observed frame interval: {_maxFrameIntervalMilliseconds:F3} ms", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Immediate Complete returns the newest result in the same frame but its call includes any unfinished worker time. " +
                "Deferred mode keeps the main thread free and only calls Complete after IsCompleted, trading result latency for overlap. " +
                "Complete is still required before reading or reusing the NativeArray.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ScheduleJob()
        {
            long startedAt = Stopwatch.GetTimestamp();
            _scheduledTimestamp = startedAt;
            _handle = new BurstParallelJob { values = _values }
                .Schedule(_values.Length, 64);
            JobHandle.ScheduleBatchedJobs();
            _lastScheduleMilliseconds = ElapsedMilliseconds(startedAt);

            _jobOutstanding = true;
            _scheduledFrame = Time.frameCount;
        }

        private void CompleteJob()
        {
            long startedAt = Stopwatch.GetTimestamp();
            _handle.Complete();
            _lastCompleteCallMilliseconds = ElapsedMilliseconds(startedAt);
            _lastJobWallMilliseconds = ElapsedMilliseconds(_scheduledTimestamp);
            _lastLatencyFrames = Time.frameCount - _scheduledFrame;
            _sink = _values[_values.Length >> 1];
            _jobOutstanding = false;
            _completedJobs++;
        }

        private void SetMode(ExecutionMode mode)
        {
            FinishOutstandingJob();
            _mode = mode;
            ResetMetrics();
        }

        private void ReallocateValues()
        {
            FinishOutstandingJob();
            if (_values.IsCreated) _values.Dispose();
            AllocateValues();
            ResetMetrics();
        }

        private void AllocateValues()
        {
            int count = _elementCounts[_elementCountIndex];
            _values = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < count; i++)
                _values[i] = 0.25f + (i & 1023) * 0.001f;
        }

        private void WarmUpJob()
        {
            new BurstParallelJob { values = _values }
                .Schedule(_values.Length, 64).Complete();
        }

        private void FinishOutstandingJob()
        {
            if (!_jobOutstanding) return;
            _handle.Complete();
            _jobOutstanding = false;
        }

        private void ResetMetrics()
        {
            _completedJobs = 0;
            _lastLatencyFrames = 0;
            _maxFrameIntervalMilliseconds = 0f;
            _lastScheduleMilliseconds = 0.0;
            _lastCompleteCallMilliseconds = 0.0;
            _lastJobWallMilliseconds = 0.0;
            _metricsStartedAt = Time.realtimeSinceStartup;
            _ignoreNextFrameInterval = true;
        }

        private float CompletedJobsPerSecond()
        {
            float seconds = Time.realtimeSinceStartup - _metricsStartedAt;
            return seconds > 0f ? _completedJobs / seconds : 0f;
        }

        private static double ElapsedMilliseconds(long startedAt)
        {
            return (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
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
            FinishOutstandingJob();
            if (_values.IsCreated) _values.Dispose();
            if (_demoCameraObject != null) Destroy(_demoCameraObject);
            if (_sink == float.MinValue) UnityEngine.Debug.Log(_sink);
        }
    }
}
