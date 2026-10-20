using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PerfLab.Samples.ResourceLifecycle
{
    public sealed class ResourceLifecycleDemo : MonoBehaviour
    {
        private const string ResourcePath = "LoadExperimentPayload";
        private const int LookupCount = 10000;
        private const int ObjectCount = 500;
        private const int ChildrenPerObject = 8;

        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private readonly List<GameObject> _instances = new List<GameObject>(ObjectCount);
        private readonly List<GameObject> _pool = new List<GameObject>(ObjectCount);

        private TextAsset _payload;
        private GameObject _template;
        private bool _busy;
        private string _lastOperation = "None";
        private double _lastMilliseconds;
        private double _perOperationMicroseconds;
        private int _sink;

        private void Awake()
        {
            _template = BuildTemplate();
        }

        private void Update()
        {
            if (_busy) return;

            if (Input.GetKeyDown(KeyCode.Alpha0)) StartCoroutine(LoadAfterUnityUnload());
            if (Input.GetKeyDown(KeyCode.Alpha1)) MeasureCachedLookups();
            if (Input.GetKeyDown(KeyCode.Alpha2)) StartCoroutine(MeasureInstantiation());
            if (Input.GetKeyDown(KeyCode.Alpha3)) StartCoroutine(MeasurePoolReuse());
        }

        private void OnGUI()
        {
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(650f), _gui.Box);
            GUILayout.Label("Resource and object lifecycle", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] Unity unload + one Resources.Load", _gui.Label);
            GUILayout.Label($"[1] Cached Resources.Load x {LookupCount:N0}", _gui.Label);
            GUILayout.Label($"[2] Instantiate + activate x {ObjectCount:N0}", _gui.Label);
            GUILayout.Label($"[3] Prewarmed pool acquire/release x {ObjectCount:N0}", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("State: " + (_busy ? "Preparing / running" : "Ready"), _gui.Label);
            GUILayout.Label("Last: " + _lastOperation, _gui.Label);
            GUILayout.Label($"Total: {_lastMilliseconds:F4} ms", _gui.Label);
            GUILayout.Label($"Per operation: {_perOperationMicroseconds:F3} us", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Mode 0 clears Unity's loaded-object cache, but cannot guarantee an OS-level cold disk read. " +
                "Mode 2 measures construction and hierarchy activation; deferred destruction is deliberately outside the timer. " +
                "Mode 3 excludes pool warm-up and measures reuse only.",
                _gui.Label);
            GUILayout.EndArea();
        }

        private IEnumerator LoadAfterUnityUnload()
        {
            _busy = true;
            if (_payload != null)
            {
                Resources.UnloadAsset(_payload);
                _payload = null;
            }

            yield return Resources.UnloadUnusedAssets();
            yield return null;

            Stopwatch stopwatch = Stopwatch.StartNew();
            _payload = Resources.Load<TextAsset>(ResourcePath);
            stopwatch.Stop();
            _sink ^= _payload != null ? _payload.bytes.Length : 0;
            Record("Unity-unloaded Resources.Load", stopwatch, 1);
            _busy = false;
        }

        private void MeasureCachedLookups()
        {
            if (_payload == null) _payload = Resources.Load<TextAsset>(ResourcePath);

            Stopwatch stopwatch = Stopwatch.StartNew();
            int value = 0;
            for (int i = 0; i < LookupCount; i++)
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
                value ^= asset.GetInstanceID();
            }
            stopwatch.Stop();
            _sink ^= value;
            Record("Cached Resources.Load lookup", stopwatch, LookupCount);
        }

        private IEnumerator MeasureInstantiation()
        {
            _busy = true;
            _instances.Clear();

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < ObjectCount; i++)
            {
                GameObject instance = Instantiate(_template);
                instance.SetActive(true);
                _instances.Add(instance);
            }
            stopwatch.Stop();
            Record("Instantiate and activate", stopwatch, ObjectCount);

            yield return null;
            for (int i = 0; i < _instances.Count; i++) Destroy(_instances[i]);
            _instances.Clear();
            _busy = false;
        }

        private IEnumerator MeasurePoolReuse()
        {
            _busy = true;
            while (_pool.Count < ObjectCount)
            {
                GameObject instance = Instantiate(_template);
                instance.SetActive(false);
                _pool.Add(instance);
            }

            yield return null;

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < ObjectCount; i++) _pool[i].SetActive(true);
            for (int i = 0; i < ObjectCount; i++) _pool[i].SetActive(false);
            stopwatch.Stop();
            Record("Prewarmed pool acquire and release", stopwatch, ObjectCount);
            _busy = false;
        }

        private GameObject BuildTemplate()
        {
            var root = new GameObject("Lifecycle Template");
            root.SetActive(false);
            for (int i = 0; i < ChildrenPerObject; i++)
            {
                var child = new GameObject("Child " + i);
                child.transform.SetParent(root.transform, false);
            }
            return root;
        }

        private void Record(string operation, Stopwatch stopwatch, int operationCount)
        {
            _lastOperation = operation;
            _lastMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            _perOperationMicroseconds = _lastMilliseconds * 1000.0 / operationCount;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _instances.Count; i++)
                if (_instances[i] != null) Destroy(_instances[i]);
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) Destroy(_pool[i]);
            if (_template != null) Destroy(_template);
        }
    }
}
