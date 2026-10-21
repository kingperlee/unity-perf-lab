using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.PhysicsTransformSync
{
    internal enum PhysicsTransformSyncKind
    {
        BatchExplicitSync,
        InterleavedExplicitSync,
        InterleavedAutoSync
    }

    internal sealed class PhysicsTransformSyncScenario : IBenchmarkScenario
    {
        private readonly PhysicsTransformSyncKind _kind;
        private Transform[] _transforms;
        private GameObject _root;
        private bool _originalAutoSyncTransforms;
        private bool _raised;
        private int _hitSink;

        public PhysicsTransformSyncScenario(PhysicsTransformSyncKind kind)
        {
            _kind = kind;
        }

        public string Name => "PhysicsTransformSync-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _originalAutoSyncTransforms = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = _kind == PhysicsTransformSyncKind.InterleavedAutoSync;

            int count = configuration.operationsPerStep;
            _transforms = new Transform[count];
            _root = new GameObject(Name + " Colliders");
            for (int i = 0; i < count; i++)
            {
                var colliderObject = new GameObject("Collider " + i);
                colliderObject.transform.SetParent(_root.transform, false);
                colliderObject.transform.position = Position(i, false);
                colliderObject.AddComponent<BoxCollider>();
                _transforms[i] = colliderObject.transform;
            }
            Physics.SyncTransforms();
        }

        public void ExecuteStep()
        {
            _raised = !_raised;
            int hits = 0;

            if (_kind == PhysicsTransformSyncKind.BatchExplicitSync)
            {
                for (int i = 0; i < _transforms.Length; i++)
                    _transforms[i].position = Position(i, _raised);

                Physics.SyncTransforms();

                for (int i = 0; i < _transforms.Length; i++)
                    if (QueryLane(i)) hits++;
            }
            else
            {
                bool explicitSync = _kind == PhysicsTransformSyncKind.InterleavedExplicitSync;
                for (int i = 0; i < _transforms.Length; i++)
                {
                    _transforms[i].position = Position(i, _raised);
                    if (explicitSync) Physics.SyncTransforms();
                    if (QueryLane(i)) hits++;
                }
            }

            _hitSink ^= hits;
        }

        public void Cleanup()
        {
            Physics.autoSyncTransforms = _originalAutoSyncTransforms;
            _transforms = null;
            if (_root != null)
            {
                _root.SetActive(false);
                Object.Destroy(_root);
                _root = null;
            }
            if (_hitSink == int.MinValue) Debug.Log(_hitSink);
        }

        private static Vector3 Position(int index, bool raised)
        {
            return new Vector3(index * 2f, raised ? 100f : 0f, 10f);
        }

        private static bool QueryLane(int index)
        {
            return Physics.Raycast(
                new Vector3(index * 2f, 0f, 0f),
                Vector3.forward,
                20f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
