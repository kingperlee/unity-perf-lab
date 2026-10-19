using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace PerfLab.Benchmarking
{
    [DefaultExecutionOrder(10000)]
    public sealed class BenchmarkHarness : MonoBehaviour
    {
        private IBenchmarkScenario _scenario;
        private BenchmarkConfiguration _configuration;
        private long[] _elapsedTicks;
        private long[] _allocatedBytes;
        private int _completedWarmupFrames;
        private int _completedSampleFrames;
        private bool _cleanupAttempted;
        private ProfilerRecorder _frameGcAllocated;

        public BenchmarkState State { get; private set; } = BenchmarkState.Idle;
        public BenchmarkResult LastResult { get; private set; }
        public Exception LastFailure { get; private set; }
        public int CompletedSamples => _completedSampleFrames;

        public void Run(IBenchmarkScenario scenario, BenchmarkConfiguration configuration)
        {
            if (State == BenchmarkState.SettingUp || State == BenchmarkState.WarmingUp ||
                State == BenchmarkState.Sampling || State == BenchmarkState.Finalizing)
            {
                throw new InvalidOperationException("A benchmark is already running.");
            }

            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            configuration.Validate();
            // Freeze the caller's mutable configuration for a reproducible run.
            _configuration = new BenchmarkConfiguration
            {
                warmupFrames = configuration.warmupFrames,
                sampleFrames = configuration.sampleFrames,
                operationsPerStep = configuration.operationsPerStep,
                randomSeed = configuration.randomSeed
            };
            _elapsedTicks = new long[_configuration.sampleFrames];
            _allocatedBytes = new long[_configuration.sampleFrames];
            _completedWarmupFrames = 0;
            _completedSampleFrames = 0;
            _cleanupAttempted = false;
            LastResult = null;
            LastFailure = null;

            try
            {
                State = BenchmarkState.SettingUp;
                _scenario.Setup(_configuration);
                if (_scenario.MeasurementMode == BenchmarkMeasurementMode.FrameInterval)
                {
                    _frameGcAllocated = ProfilerRecorder.StartNew(
                        ProfilerCategory.Memory,
                        "GC Allocated In Frame",
                        2);
                }
                State = _configuration.warmupFrames > 0
                    ? BenchmarkState.WarmingUp
                    : BenchmarkState.Sampling;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Update()
        {
            if (State == BenchmarkState.WarmingUp)
            {
                ExecuteWarmup();
            }
            else if (State == BenchmarkState.Sampling)
            {
                ExecuteSample();
            }
        }

        private void ExecuteWarmup()
        {
            try
            {
                _scenario.ExecuteStep();
                _completedWarmupFrames++;
                if (_completedWarmupFrames >= _configuration.warmupFrames)
                {
                    State = BenchmarkState.Sampling;
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void ExecuteSample()
        {
            try
            {
                long elapsed;
                long allocated;
                if (_scenario.MeasurementMode == BenchmarkMeasurementMode.FrameInterval)
                {
                    _scenario.ExecuteStep();
                    elapsed = (long)(Time.unscaledDeltaTime * Stopwatch.Frequency);
                    allocated = _frameGcAllocated.Valid ? _frameGcAllocated.LastValue : 0L;
                }
                else
                {
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    long startedAt = Stopwatch.GetTimestamp();
                    _scenario.ExecuteStep();
                    elapsed = Stopwatch.GetTimestamp() - startedAt;
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                }

                _elapsedTicks[_completedSampleFrames] = elapsed;
                _allocatedBytes[_completedSampleFrames] = allocated;
                _completedSampleFrames++;
                if (_completedSampleFrames >= _configuration.sampleFrames)
                {
                    Complete();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Complete()
        {
            State = BenchmarkState.Finalizing;
            try
            {
                LastResult = new BenchmarkResult
                {
                    scenario = _scenario.Name,
                    measurementMode = _scenario.MeasurementMode.ToString(),
                    utcTimestamp = DateTime.UtcNow.ToString("O"),
                    configuration = _configuration,
                    environment = CaptureEnvironment(),
                    elapsedMilliseconds = BenchmarkStatistics.Create(
                        _elapsedTicks,
                        1000d / Stopwatch.Frequency),
                    allocatedBytes = BenchmarkStatistics.Create(_allocatedBytes, 1d)
                };
                CleanupOnce();
                State = BenchmarkState.Completed;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Fail(Exception exception)
        {
            LastFailure = exception;
            try
            {
                CleanupOnce();
            }
            catch (Exception cleanupFailure)
            {
                LastFailure = new AggregateException(exception, cleanupFailure);
            }

            State = BenchmarkState.Failed;
            UnityEngine.Debug.LogException(LastFailure);
        }

        private void OnDestroy()
        {
            if (State == BenchmarkState.SettingUp || State == BenchmarkState.WarmingUp ||
                State == BenchmarkState.Sampling || State == BenchmarkState.Finalizing)
            {
                try { CleanupOnce(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
        }

        private void CleanupOnce()
        {
            if (_cleanupAttempted) return;
            _cleanupAttempted = true;
            if (_frameGcAllocated.Valid) _frameGcAllocated.Dispose();
            _scenario?.Cleanup();
        }

        private static BenchmarkEnvironment CaptureEnvironment()
        {
            return new BenchmarkEnvironment
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                processor = SystemInfo.processorType,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                width = Screen.width,
                height = Screen.height,
                qualityLevel = QualitySettings.GetQualityLevel()
            };
        }
    }
}
