using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PerfLab.Samples.FrameBudgeting
{
    public sealed class FrameBudgetingDemo : MonoBehaviour
    {
        private const int ObjectCount = 2000;
        private const int ChildrenPerObject = 4;

        private readonly int[] _batchSizes = { 10, 50, 200 };
        private readonly List<GameObject> _instances = new List<GameObject>(ObjectCount);
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();

        private GameObject _template;
        private GameObject _demoCameraObject;
        private Coroutine _operation;
        private Vector2 _scrollPosition;
        private int _batchSizeIndex = 1;
        private string _lastOperation = "None";
        private int _operationFrames;
        private int _completedItems;
        private double _activeWorkMilliseconds;
        private double _maxWorkSliceMilliseconds;
        private float _maxFrameIntervalMilliseconds;

        private void Awake()
        {
            EnsureCamera();
            _template = BuildTemplate();
        }

        private void Update()
        {
            if (_operation != null)
            {
                _maxFrameIntervalMilliseconds = Mathf.Max(
                    _maxFrameIntervalMilliseconds,
                    Time.unscaledDeltaTime * 1000f);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _batchSizeIndex = Mathf.Max(0, _batchSizeIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _batchSizeIndex = Mathf.Min(_batchSizes.Length - 1, _batchSizeIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0) && _instances.Count == 0)
                _operation = StartCoroutine(CreateObjects(ObjectCount));
            if (Input.GetKeyDown(KeyCode.Alpha1) && _instances.Count == 0)
                _operation = StartCoroutine(CreateObjects(_batchSizes[_batchSizeIndex]));
            if (Input.GetKeyDown(KeyCode.Alpha2) && _instances.Count > 0)
                _operation = StartCoroutine(DestroyObjects(_instances.Count));
            if (Input.GetKeyDown(KeyCode.Alpha3) && _instances.Count > 0)
                _operation = StartCoroutine(DestroyObjects(_batchSizes[_batchSizeIndex]));
        }

        private void OnGUI()
        {
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(1000f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Frame budgeting · Bulk versus spread work", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label($"[0] Create {ObjectCount:N0} in one frame", _gui.Label);
            GUILayout.Label($"[1] Create in batches of {_batchSizes[_batchSizeIndex]:N0} per frame", _gui.Label);
            GUILayout.Label("[2] Request destruction in one frame", _gui.Label);
            GUILayout.Label($"[3] Request destruction in batches of {_batchSizes[_batchSizeIndex]:N0} per frame", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: change batch size", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("State: " + (_operation == null ? "Ready" : "Running"), _gui.Label);
            GUILayout.Label($"Live objects: {_instances.Count:N0}", _gui.Label);
            GUILayout.Label($"Progress: {_completedItems:N0} / {ObjectCount:N0}", _gui.Label);
            GUILayout.Label("Last: " + _lastOperation, _gui.Label);
            GUILayout.Label($"Frames used: {_operationFrames:N0}", _gui.Label);
            GUILayout.Label($"Accumulated active work: {_activeWorkMilliseconds:F3} ms", _gui.Label);
            GUILayout.Label($"Largest work slice: {_maxWorkSliceMilliseconds:F3} ms", _gui.Label);
            GUILayout.Label($"Largest observed frame interval: {_maxFrameIntervalMilliseconds:F3} ms", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Spreading work usually does not remove the total CPU cost. It trades completion latency for a smaller per-frame spike. " +
                "Destroy() only queues destruction; Unity performs the native cleanup later, so the following frame interval is part of the observation.",
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

        private IEnumerator CreateObjects(int batchSize)
        {
            BeginOperation(batchSize == ObjectCount ? "Bulk create" : "Spread create");

            while (_instances.Count < ObjectCount)
            {
                int count = Mathf.Min(batchSize, ObjectCount - _instances.Count);
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = Instantiate(_template);
                    instance.SetActive(true);
                    _instances.Add(instance);
                }
                stopwatch.Stop();
                RecordSlice(stopwatch);
                _completedItems = _instances.Count;
                yield return null;
            }

            yield return null;
            _operation = null;
        }

        private IEnumerator DestroyObjects(int batchSize)
        {
            BeginOperation(batchSize >= _instances.Count ? "Bulk destroy request" : "Spread destroy request");
            int index = _instances.Count - 1;

            while (index >= 0)
            {
                int count = Mathf.Min(batchSize, index + 1);
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    Destroy(_instances[index]);
                    index--;
                }
                stopwatch.Stop();
                RecordSlice(stopwatch);
                _completedItems += count;
                yield return null;
            }

            _instances.Clear();
            yield return null;
            _operation = null;
        }

        private void BeginOperation(string operation)
        {
            _lastOperation = operation;
            _operationFrames = 0;
            _completedItems = 0;
            _activeWorkMilliseconds = 0.0;
            _maxWorkSliceMilliseconds = 0.0;
            _maxFrameIntervalMilliseconds = 0f;
        }

        private void RecordSlice(Stopwatch stopwatch)
        {
            double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
            _operationFrames++;
            _activeWorkMilliseconds += milliseconds;
            _maxWorkSliceMilliseconds = System.Math.Max(_maxWorkSliceMilliseconds, milliseconds);
        }

        private GameObject BuildTemplate()
        {
            var root = new GameObject("Frame Budget Template");
            root.SetActive(false);
            for (int i = 0; i < ChildrenPerObject; i++)
            {
                var child = new GameObject("Child " + i);
                child.transform.SetParent(root.transform, false);
            }
            return root;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _instances.Count; i++)
                if (_instances[i] != null) Destroy(_instances[i]);
            if (_template != null) Destroy(_template);
            if (_demoCameraObject != null) Destroy(_demoCameraObject);
        }
    }
}
