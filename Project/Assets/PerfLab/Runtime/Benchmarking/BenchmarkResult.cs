using System;

namespace PerfLab.Benchmarking
{
    [Serializable]
    public sealed class BenchmarkResult
    {
        public string scenario;
        public string utcTimestamp;
        public BenchmarkConfiguration configuration;
        public BenchmarkEnvironment environment;
        public BenchmarkDistribution elapsedMilliseconds;
        public BenchmarkDistribution allocatedBytes;
    }

    [Serializable]
    public sealed class BenchmarkEnvironment
    {
        public string unityVersion;
        public string platform;
        public string deviceModel;
        public string processor;
        public string graphicsDevice;
        public string graphicsApi;
        public int systemMemoryMb;
        public int graphicsMemoryMb;
        public int width;
        public int height;
        public int qualityLevel;
    }

    [Serializable]
    public sealed class BenchmarkDistribution
    {
        public double minimum;
        public double mean;
        public double p50;
        public double p95;
        public double p99;
        public double maximum;
    }
}
