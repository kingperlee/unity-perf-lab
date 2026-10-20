using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.TransformHierarchy
{
    internal enum TransformHierarchyKind
    {
        FlatRootMove,
        Depth16RootMove,
        FlatLeafMove,
        ReparentLeaves
    }

    internal sealed class TransformHierarchyScenario : IBenchmarkScenario
    {
        private const int ChainDepth = 16;
        private readonly TransformHierarchyKind _kind;
        private GameObject _container;
        private Transform _rootA;
        private Transform _rootB;
        private Transform[] _nodes;
        private int _count;
        private int _phase;
        private float _sink;

        public TransformHierarchyScenario(TransformHierarchyKind kind)
        {
            _kind = kind;
        }

        public string Name => "Transform-" + _kind + "-" + _count;

        public BenchmarkMeasurementMode MeasurementMode =>
            BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _count = configuration.operationsPerStep;
            _container = new GameObject(Name + " Container");
            _rootA = CreateNode("Root A", _container.transform);
            _rootB = CreateNode("Root B", _container.transform);
            _rootB.localPosition = new Vector3(2f, 0f, 0f);
            _nodes = new Transform[_count];

            if (_kind == TransformHierarchyKind.Depth16RootMove)
                BuildDepth16Chains();
            else
                BuildFlatLeaves();
        }

        public void ExecuteStep()
        {
            _phase++;
            switch (_kind)
            {
                case TransformHierarchyKind.FlatRootMove:
                case TransformHierarchyKind.Depth16RootMove:
                    _rootA.localPosition = new Vector3(
                        (_phase & 1) == 0 ? 0.01f : -0.01f,
                        0f,
                        0f);
                    ConsumeWorldPositions();
                    break;

                case TransformHierarchyKind.FlatLeafMove:
                    MoveEveryLeaf();
                    ConsumeWorldPositions();
                    break;

                case TransformHierarchyKind.ReparentLeaves:
                    ReparentEveryLeaf();
                    ConsumeWorldPositions();
                    break;
            }
        }

        public void Cleanup()
        {
            _nodes = null;
            _rootA = null;
            _rootB = null;
            if (_container != null)
            {
                _container.SetActive(false);
                Object.Destroy(_container);
                _container = null;
            }
            if (_sink == float.MinValue) Debug.Log(_sink);
        }

        private void BuildFlatLeaves()
        {
            for (var i = 0; i < _count; i++)
            {
                Transform node = CreateNode("Leaf " + i, _rootA);
                node.localPosition = InitialPosition(i);
                _nodes[i] = node;
            }
        }

        private void BuildDepth16Chains()
        {
            Transform parent = _rootA;
            for (var i = 0; i < _count; i++)
            {
                if (i % ChainDepth == 0) parent = _rootA;
                Transform node = CreateNode("Chain Node " + i, parent);
                node.localPosition = new Vector3(0.01f, 0.01f, 0f);
                _nodes[i] = node;
                parent = node;
            }
        }

        private void MoveEveryLeaf()
        {
            float offset = (_phase & 1) == 0 ? 0.001f : -0.001f;
            for (var i = 0; i < _nodes.Length; i++)
            {
                Vector3 position = _nodes[i].localPosition;
                position.x += offset;
                _nodes[i].localPosition = position;
            }
        }

        private void ReparentEveryLeaf()
        {
            Transform parent = (_phase & 1) == 0 ? _rootA : _rootB;
            for (var i = 0; i < _nodes.Length; i++)
                _nodes[i].SetParent(parent, false);
        }

        private void ConsumeWorldPositions()
        {
            float sum = 0f;
            for (var i = 0; i < _nodes.Length; i++)
                sum += _nodes[i].position.x;
            _sink = sum;
        }

        private static Transform CreateNode(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static Vector3 InitialPosition(int index)
        {
            return new Vector3(
                (index % 100) * 0.1f,
                (index / 100) * 0.1f,
                0f);
        }
    }
}
