using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.MassHud
{
    public sealed class MassHudDemo : MonoBehaviour
    {
        private readonly int[] _hudCounts = { 100, 500, 1000 };
        private readonly BenchmarkDemoGuiStyles _gui = new BenchmarkDemoGuiStyles();
        private BenchmarkHarness _harness;
        private int _hudCountIndex;
        private bool _hideOverlay;
        private string _exportStatus = string.Empty;
        private Vector2 _scrollPosition;

        private void Awake()
        {
            _harness = gameObject.AddComponent<BenchmarkHarness>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) _hideOverlay = !_hideOverlay;
            if (IsRunning()) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _hudCountIndex = Mathf.Max(0, _hudCountIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _hudCountIndex = Mathf.Min(_hudCounts.Length - 1, _hudCountIndex + 1);
            if (Input.GetKeyDown(KeyCode.Alpha0)) Run(MassHudKind.PerObjectCanvas);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Run(MassHudKind.CentralizedCanvas);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Run(MassHudKind.SpriteRenderers);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Run(MassHudKind.GpuInstancedBars);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Run(MassHudKind.GpuInstancedGlyphs);
            if (Input.GetKeyDown(KeyCode.E) && _harness.LastResult != null)
                _exportStatus = "Exported: " + BenchmarkResultExporter.Export(_harness.LastResult);
        }

        private void OnGUI()
        {
            if (_hideOverlay) return;
            _gui.EnsureCreated();
            GUILayout.BeginArea(_gui.GetPanelRect(790f), _gui.Box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Mass HUD case study · Step 4: GPU-instanced glyphs", _gui.Title);
            GUILayout.Space(8f);
            GUILayout.Label("[0] One Update + naive assignments per HUD Canvas", _gui.Label);
            GUILayout.Label("[1] One manager + cached unchanged text", _gui.Label);
            GUILayout.Label("[2] SpriteRenderer bars + cached 3D TextMesh names", _gui.Label);
            GUILayout.Label("[3] GPU-instanced depth/bars + unchanged 3D TextMesh names", _gui.Label);
            GUILayout.Label("[4] GPU-instanced depth/bars + multilingual glyph atlas", _gui.Label);
            GUILayout.Label("Left bracket / Right bracket: 100, 500, or 1,000 HUDs", _gui.Label);
            GUILayout.Label("[H] Hide overlay for cleaner profiling   [E] Export result", _gui.Label);
            GUILayout.Label("Camera: WASD move, Q/Space down/up, hold right mouse to look, Shift boost, R reset", _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label("HUD count: " + _hudCounts[_hudCountIndex], _gui.Label);
            GUILayout.Label("State: " + _harness.State, _gui.Label);
            if (_harness.LastResult != null)
            {
                var timing = _harness.LastResult.elapsedMilliseconds;
                var allocation = _harness.LastResult.allocatedBytes;
                GUILayout.Label("Last: " + _harness.LastResult.scenario, _gui.Label);
                GUILayout.Label($"Frame interval P50/P95/P99: {timing.p50:F3} / {timing.p95:F3} / {timing.p99:F3} ms", _gui.Label);
                GUILayout.Label($"Net frame allocation P50/P95/P99: {allocation.p50:F0} / {allocation.p95:F0} / {allocation.p99:F0} bytes", _gui.Label);
            }
            if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _gui.Label);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Modes 0 and 1 use world-space Canvas. Mode 2 uses SpriteRenderers and automatic batching. Mode 3 instances the rectangles but retains one TextMesh per name. Mode 4 prewarms a multilingual runtime font atlas and instances each visible glyph. " +
                "Missing glyphs fall back to '?'. Complex-script shaping remains a separate text-layout concern. Walls and nearer HUD cards use the same depth rule in every mode.",
                _gui.Label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(MassHudKind kind)
        {
            _exportStatus = string.Empty;
            _harness.Run(
                new MassHudScenario(kind),
                new BenchmarkConfiguration
                {
                    warmupFrames = 60,
                    sampleFrames = 300,
                    operationsPerStep = _hudCounts[_hudCountIndex],
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
