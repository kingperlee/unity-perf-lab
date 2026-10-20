using PerfLab.Benchmarking;
using PerfLab.Samples;
using UnityEngine;

namespace PerfLab.Samples.TextureUploads
{
    public sealed class TextureUploadDemo : MonoBehaviour
    {
        private readonly int[] _textureEdges = { 256, 512, 1024, 2048 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _edgeIndex = 1;
        private string _exportStatus = string.Empty;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _edgeIndex = Mathf.Max(0, _edgeIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _edgeIndex = Mathf.Min(_textureEdges.Length - 1, _edgeIndex + 1);

            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(TextureUploadKind.CpuFillOnly);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(TextureUploadKind.UploadWithoutMipmaps);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(TextureUploadKind.UploadWithMipmaps);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(TextureUploadKind.CpuFillAndUpload);

            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (IsRunning()) return;

            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(600f), _gui.Box);
            GUILayout.Label("Texture preparation/upload · Measured step", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] CPU pixel fill only", _gui.Label);
            GUILayout.Label("[1] Apply without mipmaps   [2] Apply with mipmaps", _gui.Label);
            GUILayout.Label("[3] CPU fill + Apply without mipmaps", _gui.Label);
            GUILayout.Label("Left bracket: smaller texture   Right bracket: larger texture", _gui.Label);
            GUILayout.Label("[E] Export last result", _gui.Label);
            int edge = _textureEdges[_edgeIndex];
            GUILayout.Label($"Texture: {edge} x {edge} RGBA32 ({edge * edge * 4f / (1024f * 1024f):F1} MiB base level)", _gui.Label);
            GUILayout.Label("Timing is CPU preparation/submission, not GPU completion", _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);

            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Step P50/P95/P99: {timing.p50:F4} / {timing.p95:F4} / {timing.p99:F4} ms", _gui.Label);
            }

            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.EndArea();
        }

        private void Run(TextureUploadKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new TextureUploadScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 30,
                    sampleFrames = 120,
                    operationsPerStep = _textureEdges[_edgeIndex],
                    randomSeed = 12345
                });
        }

        private bool IsRunning()
        {
            return _harness != null &&
                   (_harness.State == BenchmarkState.Calibrating ||
                    _harness.State == BenchmarkState.SettingUp ||
                    _harness.State == BenchmarkState.WarmingUp ||
                    _harness.State == BenchmarkState.Sampling ||
                    _harness.State == BenchmarkState.Finalizing);
        }
    }
}
