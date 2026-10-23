using PerfLab.Benchmarking;
using UnityEngine;
using UnityEngine.UI;

namespace PerfLab.Samples.UiRebuild
{
    internal enum UiRebuildKind
    {
        DataOnly,
        GraphicColorAndCanvasUpdate,
        FullLayoutRebuild,
        IsolatedLayoutRebuild
    }

    internal sealed class UiRebuildScenario : IBenchmarkScenario
    {
        private const int IsolatedElementCount = 50;
        private readonly UiRebuildKind _kind;
        private GameObject _root;
        private RectTransform _layoutRoot;
        private Image _targetImage;
        private LayoutElement _targetLayoutElement;
        private bool _toggle;
        private float _dataSink;

        public UiRebuildScenario(UiRebuildKind kind)
        {
            _kind = kind;
        }

        public string Name => "UiRebuild-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            int elementCount = configuration.operationsPerStep;
            _root = new GameObject(Name + " Root");

            if (_kind == UiRebuildKind.IsolatedLayoutRebuild)
            {
                int dynamicCount = Mathf.Min(IsolatedElementCount, elementCount);
                int staticCount = elementCount - dynamicCount;
                if (staticCount > 0) BuildCanvas("Static Canvas", staticCount, false);
                BuildCanvas("Dynamic Canvas", dynamicCount, true);
            }
            else
            {
                BuildCanvas("Single Canvas", elementCount, true);
            }

            Canvas.ForceUpdateCanvases();
        }

        public void ExecuteStep()
        {
            _toggle = !_toggle;
            switch (_kind)
            {
                case UiRebuildKind.DataOnly:
                    _dataSink = _toggle ? 1f : 0f;
                    break;

                case UiRebuildKind.GraphicColorAndCanvasUpdate:
                    _targetImage.color = _toggle ? Color.white : Color.gray;
                    Canvas.ForceUpdateCanvases();
                    break;

                case UiRebuildKind.FullLayoutRebuild:
                case UiRebuildKind.IsolatedLayoutRebuild:
                    _targetLayoutElement.preferredHeight = _toggle ? 20f : 21f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutRoot);
                    Canvas.ForceUpdateCanvases();
                    break;
            }
        }

        public void Cleanup()
        {
            _targetImage = null;
            _targetLayoutElement = null;
            _layoutRoot = null;
            if (_root != null)
            {
                _root.SetActive(false);
                Object.Destroy(_root);
                _root = null;
            }
            if (_dataSink == float.MinValue) Debug.Log(_dataSink);
        }

        private void BuildCanvas(string name, int elementCount, bool captureTarget)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(_root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelObject = new GameObject(name + " Layout", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panel = (RectTransform)panelObject.transform;
            panel.sizeDelta = new Vector2(400f, elementCount * 20f);
            var layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 1f;

            for (int i = 0; i < elementCount; i++)
            {
                var itemObject = new GameObject(
                    "Item " + i,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement));
                itemObject.transform.SetParent(panel, false);
                var image = itemObject.GetComponent<Image>();
                image.color = (i & 1) == 0 ? Color.gray : Color.white;
                image.raycastTarget = false;
                var layoutElement = itemObject.GetComponent<LayoutElement>();
                layoutElement.preferredHeight = 20f;

                if (captureTarget && i == elementCount - 1)
                {
                    _layoutRoot = panel;
                    _targetImage = image;
                    _targetLayoutElement = layoutElement;
                }
            }
        }
    }
}
