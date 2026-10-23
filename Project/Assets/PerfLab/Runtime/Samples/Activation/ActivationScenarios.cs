using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.Activation
{
    internal enum ActivationKind
    {
        DataFlags,
        BehaviourEnabled,
        IndividualSetActive,
        ParentSetActive
    }

    internal sealed class ActivationScenario : IBenchmarkScenario
    {
        private readonly ActivationKind _kind;
        private bool[] _flags;
        private ActivationProbe[] _probes;
        private GameObject[] _objects;
        private GameObject _root;
        private int _sink;

        public ActivationScenario(ActivationKind kind)
        {
            _kind = kind;
        }

        public string Name => "Activation-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            int count = configuration.operationsPerStep;
            _flags = new bool[count];
            _probes = new ActivationProbe[count];
            _objects = new GameObject[count];
            _root = new GameObject(Name + " Root");

            for (int i = 0; i < count; i++)
            {
                _flags[i] = true;
                var child = new GameObject("Activation Node " + i);
                child.transform.SetParent(_root.transform, false);
                _objects[i] = child;
                _probes[i] = child.AddComponent<ActivationProbe>();
            }
        }

        public void ExecuteStep()
        {
            switch (_kind)
            {
                case ActivationKind.DataFlags:
                    int changes = 0;
                    for (int i = 0; i < _flags.Length; i++)
                    {
                        _flags[i] = false;
                        if (!_flags[i]) changes++;
                        _flags[i] = true;
                        if (_flags[i]) changes++;
                    }
                    _sink ^= changes;
                    break;

                case ActivationKind.BehaviourEnabled:
                    for (int i = 0; i < _probes.Length; i++)
                    {
                        _probes[i].enabled = false;
                        _probes[i].enabled = true;
                    }
                    _sink ^= ActivationProbe.CallbackCount;
                    break;

                case ActivationKind.IndividualSetActive:
                    for (int i = 0; i < _objects.Length; i++)
                    {
                        _objects[i].SetActive(false);
                        _objects[i].SetActive(true);
                    }
                    _sink ^= ActivationProbe.CallbackCount;
                    break;

                case ActivationKind.ParentSetActive:
                    _root.SetActive(false);
                    _root.SetActive(true);
                    _sink ^= ActivationProbe.CallbackCount;
                    break;
            }
        }

        public void Cleanup()
        {
            _flags = null;
            _probes = null;
            _objects = null;
            if (_root != null)
            {
                _root.SetActive(false);
                Object.Destroy(_root);
                _root = null;
            }
            if (_sink == int.MinValue) Debug.Log(_sink);
        }
    }

    internal sealed class ActivationProbe : MonoBehaviour
    {
        public static int CallbackCount { get; private set; }

        private void OnEnable()
        {
            CallbackCount++;
        }

        private void OnDisable()
        {
            CallbackCount++;
        }
    }
}
