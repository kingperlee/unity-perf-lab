using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.ReferenceLookup
{
    internal enum ReferenceLookupKind
    {
        CachedComponent,
        GetComponent,
        TryGetComponent,
        CachedChildTransform,
        TransformFindDirectChild,
        TransformFindPath,
        GameObjectFind
    }

    internal sealed class ReferenceLookupScenario : IBenchmarkScenario
    {
        private const string DirectTargetName = "Lookup Direct Target";
        private const string GlobalTargetName = "Lookup Global Target Unique";
        private const string NestedPath = "Depth0/Depth1/Depth2/Depth3/Depth4/Depth5/Depth6/Depth7/Target";

        private readonly ReferenceLookupKind _kind;
        private GameObject _root;
        private GameObject _componentHost;
        private LookupMarker _cachedComponent;
        private Transform _cachedChild;
        private Transform _directParent;
        private Transform _nestedParent;
        private int _operations;
        private int _hitSink;

        public ReferenceLookupScenario(ReferenceLookupKind kind)
        {
            _kind = kind;
        }

        public string Name => "ReferenceLookup-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _operations = configuration.operationsPerStep;
            _root = new GameObject(Name + " Root");

            _componentHost = new GameObject("Component Host");
            _componentHost.transform.SetParent(_root.transform, false);
            for (int i = 0; i < 8; i++) _componentHost.AddComponent<LookupFiller>();
            _cachedComponent = _componentHost.AddComponent<LookupMarker>();

            var directRoot = new GameObject("Direct Children");
            directRoot.transform.SetParent(_root.transform, false);
            _directParent = directRoot.transform;
            for (int i = 0; i < 63; i++)
            {
                var child = new GameObject("Distractor " + i);
                child.transform.SetParent(_directParent, false);
            }
            var directTarget = new GameObject(DirectTargetName);
            directTarget.transform.SetParent(_directParent, false);
            _cachedChild = directTarget.transform;

            var nestedRoot = new GameObject("Nested Root");
            nestedRoot.transform.SetParent(_root.transform, false);
            _nestedParent = nestedRoot.transform;
            Transform parent = _nestedParent;
            for (int depth = 0; depth < 8; depth++)
            {
                var child = new GameObject("Depth" + depth);
                child.transform.SetParent(parent, false);
                parent = child.transform;
            }
            new GameObject("Target").transform.SetParent(parent, false);

            var globalRoot = new GameObject("Global Search Objects");
            globalRoot.transform.SetParent(_root.transform, false);
            for (int i = 0; i < 1000; i++)
            {
                var distractor = new GameObject("Global Distractor " + i);
                distractor.transform.SetParent(globalRoot.transform, false);
            }
            new GameObject(GlobalTargetName).transform.SetParent(globalRoot.transform, false);
        }

        public void ExecuteStep()
        {
            int hits = 0;
            switch (_kind)
            {
                case ReferenceLookupKind.CachedComponent:
                    for (int i = 0; i < _operations; i++)
                        if (!ReferenceEquals(_cachedComponent, null)) hits++;
                    break;

                case ReferenceLookupKind.GetComponent:
                    for (int i = 0; i < _operations; i++)
                    {
                        LookupMarker marker = _componentHost.GetComponent<LookupMarker>();
                        if (!ReferenceEquals(marker, null)) hits++;
                    }
                    break;

                case ReferenceLookupKind.TryGetComponent:
                    for (int i = 0; i < _operations; i++)
                        if (_componentHost.TryGetComponent(out LookupMarker marker) &&
                            !ReferenceEquals(marker, null)) hits++;
                    break;

                case ReferenceLookupKind.CachedChildTransform:
                    for (int i = 0; i < _operations; i++)
                        if (!ReferenceEquals(_cachedChild, null)) hits++;
                    break;

                case ReferenceLookupKind.TransformFindDirectChild:
                    for (int i = 0; i < _operations; i++)
                    {
                        Transform child = _directParent.Find(DirectTargetName);
                        if (!ReferenceEquals(child, null)) hits++;
                    }
                    break;

                case ReferenceLookupKind.TransformFindPath:
                    for (int i = 0; i < _operations; i++)
                    {
                        Transform child = _nestedParent.Find(NestedPath);
                        if (!ReferenceEquals(child, null)) hits++;
                    }
                    break;

                case ReferenceLookupKind.GameObjectFind:
                    for (int i = 0; i < _operations; i++)
                    {
                        GameObject found = GameObject.Find(GlobalTargetName);
                        if (!ReferenceEquals(found, null)) hits++;
                    }
                    break;
            }

            _hitSink ^= hits;
        }

        public void Cleanup()
        {
            _cachedComponent = null;
            _cachedChild = null;
            _componentHost = null;
            _directParent = null;
            _nestedParent = null;
            if (_root != null)
            {
                _root.SetActive(false);
                Object.Destroy(_root);
                _root = null;
            }
            if (_hitSink == int.MinValue) Debug.Log(_hitSink);
        }
    }

    internal sealed class LookupFiller : MonoBehaviour
    {
    }

    internal sealed class LookupMarker : MonoBehaviour
    {
    }
}
