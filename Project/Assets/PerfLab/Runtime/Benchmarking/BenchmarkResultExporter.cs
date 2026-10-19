using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PerfLab.Benchmarking
{
    public static class BenchmarkResultExporter
    {
        public static string Export(BenchmarkResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string directory = GetExportDirectory();
            Directory.CreateDirectory(directory);
            string safeName = MakeSafeFileName(result.scenario);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(directory, timestamp + "-" + safeName + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            return path;
        }

        public static string GetExportDirectory()
        {
#if UNITY_EDITOR
            // Application.dataPath is <project>/Assets while running in Editor.
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "BenchmarkResults"));
#else
            return Path.Combine(Application.persistentDataPath, "Benchmarks");
#endif
        }

        private static string MakeSafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                builder.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            }
            return builder.ToString();
        }
    }
}
