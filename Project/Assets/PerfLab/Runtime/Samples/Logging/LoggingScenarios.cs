using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.Logging
{
    internal enum LoggingKind
    {
        Baseline,
        DisabledEagerConcat,
        DisabledGuardedConcat,
        DisabledEagerFormat,
        DisabledGuardedFormat
    }

    internal sealed class LoggingScenario : IBenchmarkScenario
    {
        private readonly LoggingKind _kind;
        private bool _previousLogEnabled;
        private int _operations;
        private int _sink;

        public LoggingScenario(LoggingKind kind)
        {
            _kind = kind;
        }

        public string Name => "Logging-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _operations = configuration.operationsPerStep;
            _previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
        }

        public void ExecuteStep()
        {
            switch (_kind)
            {
                case LoggingKind.Baseline:
                    for (int i = 0; i < _operations; i++)
                        _sink = (_sink * 31) ^ i;
                    break;

                case LoggingKind.DisabledEagerConcat:
                    for (int i = 0; i < _operations; i++)
                    {
                        int health = 100 - (i & 63);
                        Debug.Log("Entity " + i + " health " + health);
                    }
                    break;

                case LoggingKind.DisabledGuardedConcat:
                    for (int i = 0; i < _operations; i++)
                    {
                        int health = 100 - (i & 63);
                        if (Debug.unityLogger.logEnabled)
                            Debug.Log("Entity " + i + " health " + health);
                        _sink ^= health;
                    }
                    break;

                case LoggingKind.DisabledEagerFormat:
                    for (int i = 0; i < _operations; i++)
                    {
                        int health = 100 - (i & 63);
                        Debug.LogFormat("Entity {0} health {1}", i, health);
                    }
                    break;

                case LoggingKind.DisabledGuardedFormat:
                    for (int i = 0; i < _operations; i++)
                    {
                        int health = 100 - (i & 63);
                        if (Debug.unityLogger.logEnabled)
                            Debug.LogFormat("Entity {0} health {1}", i, health);
                        _sink ^= health;
                    }
                    break;
            }
        }

        public void Cleanup()
        {
            Debug.unityLogger.logEnabled = _previousLogEnabled;
            if (_sink == int.MinValue) Debug.Log(_sink);
        }
    }
}
