using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.PhysicsQueries
{
    internal enum PhysicsQueryKind
    {
        RaycastSingle,
        RaycastAll,
        RaycastNonAlloc,
        OverlapSphere,
        OverlapSphereNonAlloc
    }

    internal sealed class PhysicsQueryScenario : IBenchmarkScenario
    {
        private const int ColumnCount = 8;
        private const int DepthCount = 32;
        private const int TargetLayer = 8;
        private const int DistractorLayer = 9;
        private readonly PhysicsQueryKind _kind;
        private readonly bool _useTargetMask;
        private GameObject _root;
        private RaycastHit[] _raycastBuffer;
        private Collider[] _overlapBuffer;
        private int _queryCount;
        private int _hitSink;

        public PhysicsQueryScenario(PhysicsQueryKind kind, bool useTargetMask)
        {
            _kind = kind;
            _useTargetMask = useTargetMask;
        }

        public string Name => "Physics-" + _kind +
                              (_useTargetMask ? "-TargetMask" : "-AllLayers");

        public BenchmarkMeasurementMode MeasurementMode =>
            BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _queryCount = configuration.operationsPerStep;
            _hitSink = 0;
            _raycastBuffer = new RaycastHit[DepthCount * 2];
            // Deliberately larger than the maximum possible hit count so a
            // NonAlloc result cannot look faster merely because it was truncated.
            _overlapBuffer = new Collider[ColumnCount * DepthCount * 2];
            _root = new GameObject(Name + " Colliders");

            for (var depth = 0; depth < DepthCount; depth++)
            {
                for (var column = 0; column < ColumnCount; column++)
                {
                    var colliderObject = new GameObject(
                        "Collider " + column + "-" + depth);
                    colliderObject.layer = (depth & 1) == 0
                        ? TargetLayer
                        : DistractorLayer;
                    colliderObject.transform.SetParent(_root.transform, false);
                    colliderObject.transform.position = new Vector3(
                        ColumnX(column),
                        0f,
                        1f + depth * 2f);
                    colliderObject.AddComponent<BoxCollider>();
                }
            }

            Physics.SyncTransforms();
        }

        public void ExecuteStep()
        {
            int layerMask = _useTargetMask ? 1 << TargetLayer : Physics.AllLayers;
            int hits = 0;
            for (var query = 0; query < _queryCount; query++)
            {
                switch (_kind)
                {
                    case PhysicsQueryKind.RaycastSingle:
                        if (Physics.Raycast(
                                RayOrigin(query),
                                Vector3.forward,
                                70f,
                                layerMask,
                                QueryTriggerInteraction.Ignore))
                        {
                            hits++;
                        }
                        break;

                    case PhysicsQueryKind.RaycastAll:
                        hits += Physics.RaycastAll(
                            RayOrigin(query),
                            Vector3.forward,
                            70f,
                            layerMask,
                            QueryTriggerInteraction.Ignore).Length;
                        break;

                    case PhysicsQueryKind.RaycastNonAlloc:
                        hits += Physics.RaycastNonAlloc(
                            RayOrigin(query),
                            Vector3.forward,
                            _raycastBuffer,
                            70f,
                            layerMask,
                            QueryTriggerInteraction.Ignore);
                        break;

                    case PhysicsQueryKind.OverlapSphere:
                        hits += Physics.OverlapSphere(
                            new Vector3(0f, 0f, 32f),
                            36f,
                            layerMask,
                            QueryTriggerInteraction.Ignore).Length;
                        break;

                    case PhysicsQueryKind.OverlapSphereNonAlloc:
                        hits += Physics.OverlapSphereNonAlloc(
                            new Vector3(0f, 0f, 32f),
                            36f,
                            _overlapBuffer,
                            layerMask,
                            QueryTriggerInteraction.Ignore);
                        break;
                }
            }

            _hitSink = hits;
        }

        public void Cleanup()
        {
            _raycastBuffer = null;
            _overlapBuffer = null;
            if (_root == null) return;
            _root.SetActive(false);
            Object.Destroy(_root);
            _root = null;
            if (_hitSink == int.MinValue) Debug.Log(_hitSink);
        }

        private static Vector3 RayOrigin(int queryIndex)
        {
            return new Vector3(ColumnX(queryIndex % ColumnCount), 0f, -2f);
        }

        private static float ColumnX(int column)
        {
            return (column - (ColumnCount - 1) * 0.5f) * 2f;
        }
    }
}
