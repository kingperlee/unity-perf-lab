using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PerfLab.Samples.GcBehavior
{
    public sealed class GcBehaviorDemo : MonoBehaviour
    {
        private const int AllocationChunkBytes = 4096;
        private const int BurstBytes = 16 * 1024 * 1024;

        private readonly int[] _bytesPerFrame = { 32 * 1024, 256 * 1024, 1024 * 1024 };
        private readonly List<byte[]> _retained = new List<byte[]>();
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();

        private GameObject _demoCameraObject;
        private Vector2 _scrollPosition;
        private int _allocationSizeIndex = 1;
        private bool _continuousAllocation;
        private bool _hideOverlay;
        private long _garbageBytesCreated;
        private long _retainedBytes;
        private float _maxFrameIntervalMilliseconds;
        private double _lastOperationMilliseconds;
        private string _lastOperation = "None";
        private int[] _collectionBaselines;
        private int _sink;

        private void Awake()
        {
            EnsureCamera();
            ResetObservations();
        }

        private void Update()
        {
            _maxFrameIntervalMilliseconds = Mathf.Max(
                _maxFrameIntervalMilliseconds,
                Time.unscaledDeltaTime * 1000f);

            if (Input.GetKeyDown(KeyCode.Space))
                _continuousAllocation = !_continuousAllocation;
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _allocationSizeIndex = Mathf.Max(0, _allocationSizeIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _allocationSizeIndex = Mathf.Min(_bytesPerFrame.Length - 1, _allocationSizeIndex + 1);
            if (Input.GetKeyDown(KeyCode.B)) CreateGarbage(BurstBytes, "16 MiB garbage burst");
            if (Input.GetKeyDown(KeyCode.L)) RetainMemory(BurstBytes);
            if (Input.GetKeyDown(KeyCode.U)) ReleaseRetainedReferences();
            if (Input.GetKeyDown(KeyCode.C)) ForceCollection();
            if (Input.GetKeyDown(KeyCode.R)) ResetObservations();
            if (Input.GetKeyDown(KeyCode.H)) _hideOverlay = !_hideOverlay;

            if (_continuousAllocation)
                CreateGarbage(_bytesPerFrame[_allocationSizeIndex], "Continuous garbage");
        }

        private void OnGUI()
        {
            if (_hideOverlay) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Managed GC · Allocation versus collection", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[Space] Toggle garbage allocation every frame", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: bytes allocated per frame", _gui.Label);
            GUILayout.Label("[B] Create 16 MiB temporary garbage", _gui.Label);
            GUILayout.Label("[L] Retain another 16 MiB   [U] Release retained references", _gui.Label);
            GUILayout.Label("[C] Force full GC   [R] Reset observations", _gui.Label);
            GUILayout.Label("[H] Hide/show IMGUI overlay for cleaner Profiler capture", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Continuous allocation: " + (_continuousAllocation ? "On" : "Off"), _gui.Label);
            GUILayout.Label($"Garbage per frame: {_bytesPerFrame[_allocationSizeIndex] / 1024:N0} KiB", _gui.Label);
            GUILayout.Label($"Temporary garbage created since reset: {_garbageBytesCreated / (1024.0 * 1024.0):F2} MiB", _gui.Label);
            GUILayout.Label($"References deliberately retained: {_retainedBytes / (1024.0 * 1024.0):F2} MiB", _gui.Label);
            GUILayout.Label($"GC reported managed memory: {GC.GetTotalMemory(false) / (1024.0 * 1024.0):F2} MiB", _gui.Label);
            GUILayout.Label($"Collections since reset Gen0/Gen1/Gen2: {CollectionDeltaLabel(0)} / {CollectionDeltaLabel(1)} / {CollectionDeltaLabel(2)}", _gui.Label);
            GUILayout.Label($"Largest observed frame interval: {_maxFrameIntervalMilliseconds:F3} ms", _gui.Label);
            GUILayout.Label("Last: " + _lastOperation, _gui.Label);
            GUILayout.Label($"Last measured call: {_lastOperationMilliseconds:F3} ms", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Allocating memory and collecting unreachable memory are different events. Releasing references only makes objects eligible for collection. " +
                "A forced GC demonstrates a pause but is not a routine optimization. IMGUI itself may allocate, so hide it and use the Profiler for cleaner evidence.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void CreateGarbage(int byteCount, string operation)
        {
            long startedAt = Stopwatch.GetTimestamp();
            int remaining = byteCount;
            while (remaining > 0)
            {
                int size = Math.Min(AllocationChunkBytes, remaining);
                var garbage = new byte[size];
                garbage[0] = (byte)_sink;
                _sink ^= garbage[size - 1];
                remaining -= size;
            }
            _garbageBytesCreated += byteCount;
            _lastOperation = operation;
            _lastOperationMilliseconds = ElapsedMilliseconds(startedAt);
        }

        private void RetainMemory(int byteCount)
        {
            long startedAt = Stopwatch.GetTimestamp();
            int remaining = byteCount;
            while (remaining > 0)
            {
                int size = Math.Min(AllocationChunkBytes, remaining);
                var retained = new byte[size];
                retained[0] = (byte)_sink;
                _retained.Add(retained);
                remaining -= size;
            }
            _retainedBytes += byteCount;
            _lastOperation = "Retain 16 MiB";
            _lastOperationMilliseconds = ElapsedMilliseconds(startedAt);
        }

        private void ReleaseRetainedReferences()
        {
            long startedAt = Stopwatch.GetTimestamp();
            _retained.Clear();

            _retainedBytes = 0;
            _lastOperation = "Release references (no collection requested)";
            _lastOperationMilliseconds = ElapsedMilliseconds(startedAt);
        }

        private void ForceCollection()
        {
            long startedAt = Stopwatch.GetTimestamp();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _lastOperation = "Forced full collection";
            _lastOperationMilliseconds = ElapsedMilliseconds(startedAt);
        }

        private static double ElapsedMilliseconds(long startedAt)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            return elapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        private void ResetObservations()
        {
            _garbageBytesCreated = 0;
            _maxFrameIntervalMilliseconds = 0f;
            _lastOperationMilliseconds = 0.0;
            _lastOperation = "Observations reset";
            _collectionBaselines = new int[GC.MaxGeneration + 1];
            for (int generation = 0; generation < _collectionBaselines.Length; generation++)
                _collectionBaselines[generation] = GC.CollectionCount(generation);
        }

        private string CollectionDeltaLabel(int generation)
        {
            if (generation >= _collectionBaselines.Length) return "n/a";
            return (GC.CollectionCount(generation) - _collectionBaselines[generation]).ToString();
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
            _retained.Clear();
            if (_demoCameraObject != null) Destroy(_demoCameraObject);
        }
    }
}
