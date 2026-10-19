using PerfLab.Benchmarking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PerfLab.Samples.JobsBurst
{
    internal enum JobsBurstKind
    {
        MainThread,
        SingleJob,
        BurstSingleJob,
        ParallelJob,
        BurstParallelJob
    }

    internal sealed class JobsBurstScenario : IBenchmarkScenario
    {
        private readonly JobsBurstKind _kind;
        private NativeArray<float> _values;
        private int _elementCount;
        private float _sink;

        public JobsBurstScenario(JobsBurstKind kind)
        {
            _kind = kind;
        }

        public string Name => "JobsBurst-" + _kind + "-" + _elementCount;

        public BenchmarkMeasurementMode MeasurementMode =>
            BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _elementCount = configuration.operationsPerStep;
            _values = new NativeArray<float>(
                _elementCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < _elementCount; i++)
                _values[i] = 0.25f + (i & 1023) * 0.001f;
        }

        public void ExecuteStep()
        {
            switch (_kind)
            {
                case JobsBurstKind.MainThread:
                    for (var i = 0; i < _elementCount; i++)
                        _values[i] = JobsBurstWork.Transform(_values[i]);
                    break;

                case JobsBurstKind.SingleJob:
                    new SingleJob { values = _values }.Schedule().Complete();
                    break;

                case JobsBurstKind.BurstSingleJob:
                    new BurstSingleJob { values = _values }.Schedule().Complete();
                    break;

                case JobsBurstKind.ParallelJob:
                    new ParallelJob { values = _values }
                        .Schedule(_elementCount, 64).Complete();
                    break;

                case JobsBurstKind.BurstParallelJob:
                    new BurstParallelJob { values = _values }
                        .Schedule(_elementCount, 64).Complete();
                    break;
            }

            _sink = _values[_elementCount >> 1];
        }

        public void Cleanup()
        {
            if (_values.IsCreated) _values.Dispose();
            if (_sink == float.MinValue) Debug.Log(_sink);
        }
    }

    internal static class JobsBurstWork
    {
        public static float Transform(float value)
        {
            // Enough arithmetic to expose a useful scheduling crossover while
            // remaining deterministic and Burst-compatible.
            for (var iteration = 0; iteration < 16; iteration++)
                value = value * 1.000001f + 0.000003f / (value + 1.0f);
            return value;
        }
    }

    internal struct SingleJob : IJob
    {
        public NativeArray<float> values;

        public void Execute()
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = JobsBurstWork.Transform(values[i]);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct BurstSingleJob : IJob
    {
        public NativeArray<float> values;

        public void Execute()
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = JobsBurstWork.Transform(values[i]);
        }
    }

    internal struct ParallelJob : IJobParallelFor
    {
        public NativeArray<float> values;

        public void Execute(int index)
        {
            values[index] = JobsBurstWork.Transform(values[index]);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct BurstParallelJob : IJobParallelFor
    {
        public NativeArray<float> values;

        public void Execute(int index)
        {
            values[index] = JobsBurstWork.Transform(values[index]);
        }
    }
}
