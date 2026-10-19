using UnityEngine;

namespace PerfLab.Samples
{
    internal sealed class BenchmarkDemoGuiStyles
    {
        public GUIStyle Box { get; private set; }
        public GUIStyle Title { get; private set; }
        public GUIStyle Label { get; private set; }

        public void EnsureCreated()
        {
            if (Box != null) return;

            float dpiScale = Screen.dpi > 0f
                ? Mathf.Clamp(Screen.dpi / 96f, 1f, 1.75f)
                : 1.25f;
            int titleSize = Mathf.RoundToInt(22f * dpiScale);
            int labelSize = Mathf.RoundToInt(17f * dpiScale);
            int padding = Mathf.RoundToInt(18f * dpiScale);

            Box = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(padding, padding, padding, padding)
            };
            Title = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = labelSize,
                wordWrap = true
            };
        }

        public Rect GetPanelRect(float preferredHeight)
        {
            return new Rect(
                20f,
                20f,
                Mathf.Max(320f, Mathf.Min(900f, Screen.width - 40f)),
                Mathf.Max(200f, Mathf.Min(preferredHeight, Screen.height - 40f)));
        }
    }
}
