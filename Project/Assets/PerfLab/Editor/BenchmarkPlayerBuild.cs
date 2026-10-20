using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PerfLab.Editor
{
    public static class BenchmarkPlayerBuild
    {
        private const string SuiteScene =
            "Assets/PerfLab/Scenes/AutomatedBenchmarkSuite.unity";

        [MenuItem("PerfLab/Benchmarks/Build Benchmark Player")]
        public static void BuildFromMenu()
        {
            Build(false);
        }

        // Command-line entry point for Unity's -executeMethod argument.
        public static void BuildFromCommandLine()
        {
            Build(HasFlag("-benchmarkDevelopmentBuild"));
        }

        private static void Build(bool developmentBuild)
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string outputPath = GetArgumentValue("-benchmarkBuildPath");
            if (string.IsNullOrEmpty(outputPath))
                outputPath = GetDefaultOutputPath(target);
            outputPath = Path.GetFullPath(outputPath);

            string outputDirectory = IsDirectoryBuild(target)
                ? outputPath
                : Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            BuildOptions options = BuildOptions.CleanBuildCache;
            if (developmentBuild) options |= BuildOptions.Development;
            var playerOptions = new BuildPlayerOptions
            {
                scenes = new[] { SuiteScene },
                locationPathName = outputPath,
                target = target,
                options = options
            };

            Debug.Log(
                "Building benchmark player\nTarget: " + target +
                "\nDevelopment: " + developmentBuild +
                "\nOutput: " + outputPath);
            BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Benchmark Player build failed: " + summary.result +
                    " (errors: " + summary.totalErrors + ")");
            }

            Debug.Log(
                "Benchmark Player build succeeded: " + outputPath +
                " (" + summary.totalSize + " bytes, " +
                summary.totalTime + ")");
        }

        private static string GetDefaultOutputPath(BuildTarget target)
        {
            string root = Path.Combine("Builds", "Benchmark", target.ToString());
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(root, "PerfLabBenchmark.exe");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(root, "PerfLabBenchmark.app");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(root, "PerfLabBenchmark");
                case BuildTarget.Android:
                    return Path.Combine(root, "PerfLabBenchmark.apk");
                case BuildTarget.iOS:
                case BuildTarget.WebGL:
                    return root;
                default:
                    throw new NotSupportedException(
                        "No default benchmark output for target " + target +
                        ". Pass -benchmarkBuildPath explicitly.");
            }
        }

        private static bool IsDirectoryBuild(BuildTarget target)
        {
            return target == BuildTarget.iOS || target == BuildTarget.WebGL;
        }

        private static bool HasFlag(string flag)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetArgumentValue(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(
                        arguments[i],
                        argumentName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }
            return string.Empty;
        }
    }
}
