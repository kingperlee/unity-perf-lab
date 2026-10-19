using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.UpdateDispatch
{
    internal static class UpdateWorkload
    {
        public static void Step(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
        }
    }

    internal sealed class NativeUpdateAgent : MonoBehaviour
    {
        private uint _state;

        public void Initialize(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        private void Update()
        {
            UpdateWorkload.Step(ref _state);
        }
    }

    internal sealed class CentralUpdateAgent : MonoBehaviour
    {
        private uint _state;

        public void Initialize(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public void Tick()
        {
            UpdateWorkload.Step(ref _state);
        }
    }

    [DefaultExecutionOrder(-100)]
    internal sealed class CentralUpdateManager : MonoBehaviour
    {
        private CentralUpdateAgent[] _agents;

        public void Initialize(CentralUpdateAgent[] agents)
        {
            _agents = agents;
        }

        private void Update()
        {
            if (_agents == null) return;
            for (var i = 0; i < _agents.Length; i++)
            {
                _agents[i].Tick();
            }
        }
    }

    internal abstract class UpdateDispatchScenario : IBenchmarkScenario
    {
        private GameObject _root;
        protected int AgentCount { get; private set; }
        protected int RandomSeed { get; private set; }

        public abstract string Name { get; }
        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.FrameInterval;

        public void Setup(BenchmarkConfiguration configuration)
        {
            AgentCount = configuration.operationsPerStep;
            RandomSeed = configuration.randomSeed;
            _root = new GameObject(Name + " Workload");
            Build(_root.transform);
        }

        public void ExecuteStep()
        {
            // Work executes through Unity's Update dispatch before the late-running Harness.
        }

        public void Cleanup()
        {
            if (_root == null) return;
            _root.SetActive(false);
            Object.Destroy(_root);
            _root = null;
        }

        protected abstract void Build(Transform root);

        protected uint SeedFor(int index)
        {
            return unchecked((uint)(RandomSeed + index * 747796405));
        }
    }

    internal sealed class NativeUpdateScenario : UpdateDispatchScenario
    {
        public override string Name => "MonoBehaviour-Update-" + AgentCount;

        protected override void Build(Transform root)
        {
            for (var i = 0; i < AgentCount; i++)
            {
                var instance = new GameObject("Agent " + i);
                instance.transform.SetParent(root, false);
                instance.AddComponent<NativeUpdateAgent>().Initialize(SeedFor(i));
            }
        }
    }

    internal sealed class CentralUpdateScenario : UpdateDispatchScenario
    {
        public override string Name => "Central-Update-" + AgentCount;

        protected override void Build(Transform root)
        {
            var agents = new CentralUpdateAgent[AgentCount];
            for (var i = 0; i < AgentCount; i++)
            {
                var instance = new GameObject("Agent " + i);
                instance.transform.SetParent(root, false);
                agents[i] = instance.AddComponent<CentralUpdateAgent>();
                agents[i].Initialize(SeedFor(i));
            }

            root.gameObject.AddComponent<CentralUpdateManager>().Initialize(agents);
        }
    }
}
