using System;
using System.Linq;
using PerfLab.Benchmarking;

namespace PerfLab.Samples.GcAllocations
{
    internal abstract class GcAllocationScenario : IBenchmarkScenario
    {
        protected int Operations { get; private set; }
        protected long NumericSink { get; set; }

        public abstract string Name { get; }
        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public virtual void Setup(BenchmarkConfiguration configuration)
        {
            Operations = configuration.operationsPerStep;
            NumericSink = 0;
        }

        public abstract void ExecuteStep();

        public virtual void Cleanup()
        {
        }
    }

    internal sealed class BaselineLoopScenario : GcAllocationScenario
    {
        public override string Name => "GC-Baseline-Loop";

        public override void ExecuteStep()
        {
            long sum = 0;
            for (var i = 0; i < Operations; i++)
            {
                if ((i & 1) == 0) sum += i * 3L;
            }
            NumericSink = sum;
        }
    }

    internal sealed class BoxingScenario : GcAllocationScenario
    {
        private object _escapeSink;
        public override string Name => "GC-Value-Type-Boxing";

        public override void ExecuteStep()
        {
            long sum = 0;
            for (var i = 0; i < Operations; i++)
            {
                object boxed = i;
                _escapeSink = boxed;
                sum += (int)boxed;
            }
            NumericSink = sum;
        }

        public override void Cleanup()
        {
            _escapeSink = null;
        }
    }

    internal sealed class CapturingClosureScenario : GcAllocationScenario
    {
        private Func<int> _escapeSink;
        public override string Name => "GC-Capturing-Closure";

        public override void ExecuteStep()
        {
            long sum = 0;
            for (var i = 0; i < Operations; i++)
            {
                int captured = i;
                Func<int> getter = () => captured * 3;
                _escapeSink = getter;
                sum += getter();
            }
            NumericSink = sum;
        }

        public override void Cleanup()
        {
            _escapeSink = null;
        }
    }

    internal sealed class LinqPipelineScenario : GcAllocationScenario
    {
        public override string Name => "GC-LINQ-Pipeline";

        public override void ExecuteStep()
        {
            NumericSink = Enumerable.Range(0, Operations)
                .Where(value => (value & 1) == 0)
                .Select(value => value * 3L)
                .Sum();
        }
    }

    internal sealed class StringConstructionScenario : GcAllocationScenario
    {
        private string _escapeSink;
        public override string Name => "GC-String-Construction";

        public override void ExecuteStep()
        {
            for (var i = 0; i < Operations; i++)
            {
                _escapeSink = "Value=" + i + ", parity=" + (i & 1);
            }
            NumericSink = _escapeSink == null ? 0 : _escapeSink.Length;
        }

        public override void Cleanup()
        {
            _escapeSink = null;
        }
    }
}
