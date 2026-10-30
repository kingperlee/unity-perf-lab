using System;
using PerfLab.Benchmarking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PerfLab.Samples.MassHud
{
    internal enum MassHudKind
    {
        PerObjectCanvas,
        CentralizedCanvas,
        SpriteRenderers,
        GpuInstancedBars,
        GpuInstancedGlyphs
    }

    internal sealed class MassHudScenario : IBenchmarkScenario
    {
        private readonly MassHudKind _kind;
        private MassHudElement[] _elements;

        public MassHudScenario(MassHudKind kind)
        {
            _kind = kind;
        }

        public string Name => "Mass-HUD-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.FrameInterval;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _elements = MassHudSceneFactory.Build(configuration.operationsPerStep, _kind);
        }

        public void ExecuteStep()
        {
            // Both modes run through normal PlayerLoop callbacks. FrameInterval
            // captures the complete preceding frame, including Canvas rebuilds.
        }

        public void Cleanup()
        {
            // Keep the last completed scene visible so depth occlusion can be inspected.
            // The factory disables and replaces it before the next run.
            _elements = null;
        }
    }

    internal sealed class MassHudCentralUpdater : MonoBehaviour
    {
        private MassHudElement[] _elements;

        public void Initialize(MassHudElement[] elements)
        {
            _elements = elements;
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < _elements.Length; i++)
                _elements[i].UpdateFromManager(time);
        }
    }

    internal sealed class MassHudElement : MonoBehaviour
    {
        private const float HealthBarWidth = 240f;
        private Camera _camera;
        private Image _healthFill;
        private Text _nameLabel;
        private Transform _spriteHealthFill;
        private TextMesh _spriteNameLabel;
        private int _index;
        private int _level;
        private bool _perObjectUpdate;

        public void Initialize(
            int index,
            Camera targetCamera,
            Image healthFill,
            Text nameLabel,
            bool perObjectUpdate)
        {
            _index = index;
            _level = 1 + index % 99;
            _camera = targetCamera;
            _healthFill = healthFill;
            _nameLabel = nameLabel;
            _perObjectUpdate = perObjectUpdate;
            _nameLabel.text = BuildDisplayName();
            enabled = perObjectUpdate;
            Apply(Time.unscaledTime, perObjectUpdate);
        }

        public void InitializeSprite(
            int index,
            Camera targetCamera,
            Transform healthFill,
            TextMesh nameLabel)
        {
            _index = index;
            _level = 1 + index % 99;
            _camera = targetCamera;
            _spriteHealthFill = healthFill;
            _spriteNameLabel = nameLabel;
            _spriteNameLabel.text = BuildDisplayName();
            enabled = false;
            Apply(Time.unscaledTime, false);
        }

        private void Update()
        {
            Apply(Time.unscaledTime, true);
        }

        public void UpdateFromManager(float time)
        {
            Apply(time, false);
        }

        private void Apply(float time, bool emulateNaiveAssignments)
        {
            transform.rotation = _camera.transform.rotation;
            float health = 0.5f + 0.5f * Mathf.Sin(time * 1.7f + _index * 0.173f);
            if (_healthFill != null)
            {
                Vector2 fillSize = _healthFill.rectTransform.sizeDelta;
                fillSize.x = HealthBarWidth * health;
                _healthFill.rectTransform.sizeDelta = fillSize;
            }
            else
            {
                Vector3 fillScale = _spriteHealthFill.localScale;
                fillScale.x = 2.4f * health;
                _spriteHealthFill.localScale = fillScale;
                Vector3 fillPosition = _spriteHealthFill.localPosition;
                fillPosition.x = -1.2f + fillScale.x * 0.5f;
                _spriteHealthFill.localPosition = fillPosition;
            }

            // The baseline represents a common mistake: assigning unchanged text every frame.
            if (emulateNaiveAssignments && _nameLabel != null)
                _nameLabel.text = BuildDisplayName();
        }

        private string BuildDisplayName()
        {
            return "Player " + _index + "  Lv." + _level;
        }
    }

    internal sealed class MassHudInstancedRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;
        private static readonly Color BackgroundColor = new Color(0.03f, 0.03f, 0.03f, 0.92f);
        private static readonly Color HealthBackColor = new Color(0.18f, 0.04f, 0.04f, 1f);
        private static readonly Color HealthFillColor = new Color(0.15f, 0.9f, 0.25f, 1f);

        private Camera _camera;
        private Vector3[] _anchors;
        private Transform[] _nameTransforms;
        private Matrix4x4[] _depthMatrices;
        private Matrix4x4[] _backgroundMatrices;
        private Matrix4x4[] _healthBackMatrices;
        private Matrix4x4[] _healthFillMatrices;
        private Matrix4x4[] _drawBuffer;
        private Mesh _quadMesh;
        private Material _depthMaterial;
        private Material _backgroundMaterial;
        private Material _healthBackMaterial;
        private Material _healthFillMaterial;

        public void Initialize(
            Camera targetCamera,
            Vector3[] anchors,
            Font font,
            Material textMaterial,
            Material depthMaterial,
            Shader colorShader,
            bool createTextMeshes)
        {
            _camera = targetCamera;
            _anchors = anchors;
            _nameTransforms = createTextMeshes ? new Transform[anchors.Length] : null;
            _depthMatrices = new Matrix4x4[anchors.Length];
            _backgroundMatrices = new Matrix4x4[anchors.Length];
            _healthBackMatrices = new Matrix4x4[anchors.Length];
            _healthFillMatrices = new Matrix4x4[anchors.Length];
            _drawBuffer = new Matrix4x4[Mathf.Min(MaxInstancesPerDraw, anchors.Length)];
            _quadMesh = CreateQuadMesh();

            _depthMaterial = new Material(depthMaterial)
            {
                name = "Mass HUD Instanced Depth Material",
                enableInstancing = true
            };
            _backgroundMaterial = CreateColorMaterial(colorShader, "Background", BackgroundColor, 3000);
            _healthBackMaterial = CreateColorMaterial(colorShader, "Health Back", HealthBackColor, 3001);
            _healthFillMaterial = CreateColorMaterial(colorShader, "Health Fill", HealthFillColor, 3002);

            if (createTextMeshes)
            {
                for (int i = 0; i < anchors.Length; i++)
                    _nameTransforms[i] = CreateName(transform, font, textMaterial, anchors[i], i);
            }

            UpdateInstanceData(Time.unscaledTime);
        }

        private void LateUpdate()
        {
            UpdateInstanceData(Time.unscaledTime);
            Draw(_depthMatrices, _depthMaterial);
            Draw(_backgroundMatrices, _backgroundMaterial);
            Draw(_healthBackMatrices, _healthBackMaterial);
            Draw(_healthFillMatrices, _healthFillMaterial);
        }

        private void UpdateInstanceData(float time)
        {
            Quaternion rotation = _camera.transform.rotation;
            for (int i = 0; i < _anchors.Length; i++)
            {
                Vector3 anchor = _anchors[i];
                float health = 0.5f + 0.5f * Mathf.Sin(time * 1.7f + i * 0.173f);
                float fillWidth = 2.4f * health;

                if (_nameTransforms != null)
                {
                    _nameTransforms[i].SetPositionAndRotation(
                        anchor + rotation * new Vector3(0f, 0.14f, -0.025f),
                        rotation);
                }
                _depthMatrices[i] = CreateMatrix(anchor, rotation, Vector3.zero, new Vector3(2.65f, 0.7f, 1f));
                _backgroundMatrices[i] = CreateMatrix(anchor, rotation, new Vector3(0f, 0f, -0.01f), new Vector3(2.6f, 0.68f, 1f));
                _healthBackMatrices[i] = CreateMatrix(anchor, rotation, new Vector3(0f, -0.18f, -0.015f), new Vector3(2.4f, 0.18f, 1f));
                _healthFillMatrices[i] = CreateMatrix(
                    anchor,
                    rotation,
                    new Vector3(-1.2f + fillWidth * 0.5f, -0.18f, -0.02f),
                    new Vector3(fillWidth, 0.18f, 1f));
            }
        }

        private void Draw(Matrix4x4[] matrices, Material material)
        {
            if (matrices.Length <= MaxInstancesPerDraw)
            {
                Graphics.DrawMeshInstanced(
                    _quadMesh,
                    0,
                    material,
                    matrices,
                    matrices.Length,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    _camera,
                    LightProbeUsage.Off);
                return;
            }

            for (int offset = 0; offset < matrices.Length; offset += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, matrices.Length - offset);
                Array.Copy(matrices, offset, _drawBuffer, 0, count);
                Graphics.DrawMeshInstanced(
                    _quadMesh,
                    0,
                    material,
                    _drawBuffer,
                    count,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    _camera,
                    LightProbeUsage.Off);
            }
        }

        private static Matrix4x4 CreateMatrix(
            Vector3 anchor,
            Quaternion rotation,
            Vector3 localPosition,
            Vector3 localScale)
        {
            return Matrix4x4.TRS(anchor + rotation * localPosition, rotation, localScale);
        }

        private static Transform CreateName(
            Transform parent,
            Font font,
            Material textMaterial,
            Vector3 anchor,
            int index)
        {
            var nameObject = new GameObject("Name " + index);
            nameObject.transform.SetParent(parent, false);
            nameObject.transform.position = anchor;
            var label = nameObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 32;
            label.characterSize = 0.06f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.text = "Player " + index + "  Lv." + (1 + index % 99);
            MeshRenderer renderer = nameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = textMaterial;
            renderer.sortingOrder = 3;
            return nameObject.transform;
        }

        private static Material CreateColorMaterial(
            Shader shader,
            string label,
            Color color,
            int renderQueue)
        {
            var material = new Material(shader)
            {
                name = "Mass HUD Instanced " + label + " Material",
                color = color,
                enableInstancing = true,
                renderQueue = renderQueue
            };
            return material;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "Mass HUD Instanced Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void OnDestroy()
        {
            Destroy(_quadMesh);
            Destroy(_depthMaterial);
            Destroy(_backgroundMaterial);
            Destroy(_healthBackMaterial);
            Destroy(_healthFillMaterial);
        }
    }

    internal static class MassHudSceneFactory
    {
        private const string RuntimeRootName = "Mass HUD Runtime";
        private static GameObject _currentRoot;
        private static Material _currentDepthMaterial;
        private static Material _currentTextMaterial;
        private static Material _currentEnvironmentMaterial;
        private static Font _currentDynamicFont;
        private static Texture2D _currentSpriteTexture;
        private static Sprite _currentSprite;

        public static MassHudElement[] Build(int count, MassHudKind kind)
        {
            RetirePreviousScene();

            _currentRoot = new GameObject(RuntimeRootName);
            Camera camera = CreateCamera(_currentRoot.transform);

            Shader depthShader = Resources.Load<Shader>("PerfLabHudDepthOnly");
            if (depthShader == null)
                throw new InvalidOperationException("Missing Resources/PerfLabHudDepthOnly.shader");
            _currentDepthMaterial = new Material(depthShader) { name = "Mass HUD Shared Depth Material" };
            Shader environmentShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (environmentShader == null)
                throw new InvalidOperationException("URP Unlit shader is unavailable; verify that URP is active.");
            _currentEnvironmentMaterial = new Material(environmentShader)
            {
                name = "Mass HUD Shared Environment Material"
            };
            _currentEnvironmentMaterial.SetColor("_BaseColor", new Color(0.32f, 0.35f, 0.34f, 1f));
            CreateEnvironment(_currentRoot.transform);
            bool usesInstancedGlyphs = kind == MassHudKind.GpuInstancedGlyphs;
            Font font = usesInstancedGlyphs ? LoadMultilingualFont() : LoadBuiltinFont();
            var elements = new MassHudElement[count];
            bool perObjectUpdate = kind == MassHudKind.PerObjectCanvas;
            bool usesTextMesh = kind == MassHudKind.SpriteRenderers || kind == MassHudKind.GpuInstancedBars;
            if (kind == MassHudKind.SpriteRenderers)
                CreateRuntimeSprite();
            if (usesTextMesh)
            {
                Shader textShader = Resources.Load<Shader>("PerfLabHudText");
                if (textShader == null)
                    throw new InvalidOperationException("Missing Resources/PerfLabHudText.shader");
                _currentTextMaterial = new Material(textShader)
                {
                    name = "Mass HUD Shared Text Material",
                    mainTexture = font.material.mainTexture
                };
            }

            if (kind == MassHudKind.GpuInstancedBars || kind == MassHudKind.GpuInstancedGlyphs)
            {
                Shader colorShader = Resources.Load<Shader>("PerfLabHudInstancedColor");
                if (colorShader == null)
                    throw new InvalidOperationException("Missing Resources/PerfLabHudInstancedColor.shader");
                var anchors = new Vector3[count];
                for (int i = 0; i < count; i++)
                    anchors[i] = GetAnchor(i);
                _currentRoot.AddComponent<MassHudInstancedRenderer>().Initialize(
                    camera,
                    anchors,
                    font,
                    _currentTextMaterial,
                    _currentDepthMaterial,
                    colorShader,
                    kind == MassHudKind.GpuInstancedBars);
                if (usesInstancedGlyphs)
                {
                    Shader glyphShader = Resources.Load<Shader>("PerfLabHudInstancedGlyph");
                    if (glyphShader == null)
                        throw new InvalidOperationException("Missing Resources/PerfLabHudInstancedGlyph.shader");
                    _currentRoot.AddComponent<MassHudGlyphInstancedRenderer>().Initialize(
                        camera,
                        anchors,
                        font,
                        glyphShader);
                }
                return Array.Empty<MassHudElement>();
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = GetAnchor(i);
                elements[i] = kind == MassHudKind.SpriteRenderers
                    ? CreateSpriteHud(
                        _currentRoot.transform,
                        camera,
                        _currentDepthMaterial,
                        font,
                        anchor,
                        i)
                    : CreateCanvasHud(
                        _currentRoot.transform,
                        camera,
                        _currentDepthMaterial,
                        font,
                        anchor,
                        i,
                        perObjectUpdate);
            }

            if (!perObjectUpdate)
                _currentRoot.AddComponent<MassHudCentralUpdater>().Initialize(elements);

            return elements;
        }

        private static Vector3 GetAnchor(int index)
        {
            const int columns = 25;
            int column = index % columns;
            int row = index / columns;
            float x = (column - (columns - 1) * 0.5f) * 1.55f;
            float y = 1.5f + (index % 7) * 0.08f;
            float z = 5f + row * 1.45f;
            return new Vector3(x, y, z);
        }

        private static MassHudElement CreateCanvasHud(
            Transform parent,
            Camera camera,
            Material depthMaterial,
            Font font,
            Vector3 anchor,
            int index,
            bool perObjectUpdate)
        {
            var hudRoot = new GameObject("HUD " + index);
            hudRoot.transform.SetParent(parent, false);
            hudRoot.transform.position = anchor;
            hudRoot.transform.rotation = camera.transform.rotation;

            var depthObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            depthObject.name = "Depth";
            depthObject.transform.SetParent(hudRoot.transform, false);
            depthObject.transform.localPosition = Vector3.zero;
            depthObject.transform.localRotation = Quaternion.identity;
            depthObject.transform.localScale = new Vector3(2.65f, 0.7f, 1f);
            UnityEngine.Object.Destroy(depthObject.GetComponent<Collider>());
            depthObject.GetComponent<MeshRenderer>().sharedMaterial = depthMaterial;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(hudRoot.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(260f, 68f);

            Image background = CreateImage(
                canvasObject.transform,
                "Background",
                new Color(0.03f, 0.03f, 0.03f, 0.92f));
            SetRect(background.rectTransform, new Vector2(0f, 0f), new Vector2(260f, 68f));

            Image healthBack = CreateImage(
                canvasObject.transform,
                "Health Back",
                new Color(0.18f, 0.04f, 0.04f, 1f));
            SetRect(healthBack.rectTransform, new Vector2(0f, -18f), new Vector2(240f, 18f));

            Image healthFill = CreateImage(
                canvasObject.transform,
                "Health Fill",
                new Color(0.15f, 0.9f, 0.25f, 1f));
            SetRect(healthFill.rectTransform, new Vector2(-120f, -18f), new Vector2(240f, 18f));
            healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            var labelObject = new GameObject("Name", typeof(RectTransform));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            SetRect(label.rectTransform, new Vector2(0f, 12f), new Vector2(250f, 34f));

            var element = hudRoot.AddComponent<MassHudElement>();
            element.Initialize(index, camera, healthFill, label, perObjectUpdate);
            return element;
        }

        private static MassHudElement CreateSpriteHud(
            Transform parent,
            Camera camera,
            Material depthMaterial,
            Font font,
            Vector3 anchor,
            int index)
        {
            GameObject hudRoot = CreateHudRoot(parent, camera, anchor, index);
            CreateDepthCard(hudRoot.transform, depthMaterial);

            SpriteRenderer background = CreateSpriteRenderer(
                hudRoot.transform,
                "Background",
                new Color(0.03f, 0.03f, 0.03f, 0.92f),
                new Vector3(0f, 0f, -0.01f),
                new Vector2(2.6f, 0.68f),
                0);
            SpriteRenderer healthBack = CreateSpriteRenderer(
                hudRoot.transform,
                "Health Back",
                new Color(0.18f, 0.04f, 0.04f, 1f),
                new Vector3(0f, -0.18f, -0.015f),
                new Vector2(2.4f, 0.18f),
                1);
            SpriteRenderer healthFill = CreateSpriteRenderer(
                hudRoot.transform,
                "Health Fill",
                new Color(0.15f, 0.9f, 0.25f, 1f),
                new Vector3(0f, -0.18f, -0.02f),
                new Vector2(2.4f, 0.18f),
                2);

            var nameObject = new GameObject("Name");
            nameObject.transform.SetParent(hudRoot.transform, false);
            nameObject.transform.localPosition = new Vector3(0f, 0.14f, -0.025f);
            var nameLabel = nameObject.AddComponent<TextMesh>();
            nameLabel.font = font;
            nameLabel.fontSize = 32;
            nameLabel.characterSize = 0.06f;
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.color = Color.white;
            var nameRenderer = nameObject.GetComponent<MeshRenderer>();
            nameRenderer.sharedMaterial = _currentTextMaterial;
            nameRenderer.sortingOrder = 3;

            var element = hudRoot.AddComponent<MassHudElement>();
            element.InitializeSprite(index, camera, healthFill.transform, nameLabel);
            return element;
        }

        private static GameObject CreateHudRoot(
            Transform parent,
            Camera camera,
            Vector3 anchor,
            int index)
        {
            var hudRoot = new GameObject("HUD " + index);
            hudRoot.transform.SetParent(parent, false);
            hudRoot.transform.position = anchor;
            hudRoot.transform.rotation = camera.transform.rotation;
            return hudRoot;
        }

        private static void CreateDepthCard(Transform parent, Material depthMaterial)
        {
            var depthObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            depthObject.name = "Depth";
            depthObject.transform.SetParent(parent, false);
            depthObject.transform.localPosition = Vector3.zero;
            depthObject.transform.localRotation = Quaternion.identity;
            depthObject.transform.localScale = new Vector3(2.65f, 0.7f, 1f);
            UnityEngine.Object.Destroy(depthObject.GetComponent<Collider>());
            depthObject.GetComponent<MeshRenderer>().sharedMaterial = depthMaterial;
        }

        private static SpriteRenderer CreateSpriteRenderer(
            Transform parent,
            string name,
            Color color,
            Vector3 localPosition,
            Vector2 size,
            int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localPosition = localPosition;
            spriteObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _currentSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void CreateRuntimeSprite()
        {
            _currentSpriteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Mass HUD White Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _currentSpriteTexture.SetPixel(0, 0, Color.white);
            _currentSpriteTexture.Apply(false, true);
            _currentSprite = Sprite.Create(
                _currentSpriteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _currentSprite.name = "Mass HUD Shared Sprite";
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Camera CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Mass HUD Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 12f, -28f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 2.5f, 35f) - cameraObject.transform.position,
                Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<MassHudCameraController>().CaptureInitialPose();
            return camera;
        }

        private static void CreateEnvironment(Transform parent)
        {
            CreateBlock(parent, "Ground", new Vector3(0f, -0.55f, 45f), new Vector3(55f, 1f, 110f));
            CreateBlock(parent, "Occluder Left", new Vector3(-8f, 3.5f, 25f), new Vector3(7f, 8f, 2f));
            CreateBlock(parent, "Occluder Right", new Vector3(9f, 5f, 48f), new Vector3(9f, 11f, 2f));
            CreateBlock(parent, "Occluder Center", new Vector3(0f, 2.5f, 70f), new Vector3(5f, 6f, 2f));
        }

        private static void CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = _currentEnvironmentMaterial;
        }

        private static Font LoadBuiltinFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
            }

            if (font != null) return font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Font LoadMultilingualFont()
        {
            string[] candidates =
            {
                "Microsoft YaHei",
                "Yu Gothic UI",
                "Malgun Gothic",
                "Noto Sans CJK SC",
                "PingFang SC",
                "Arial Unicode MS",
                "Segoe UI",
                "Arial"
            };
            _currentDynamicFont = Font.CreateDynamicFontFromOSFont(candidates, 64);
            return _currentDynamicFont != null ? _currentDynamicFont : LoadBuiltinFont();
        }

        private static void RetirePreviousScene()
        {
            if (_currentRoot == null) return;
            _currentRoot.SetActive(false);
            UnityEngine.Object.Destroy(_currentRoot);
            if (_currentDepthMaterial != null)
                UnityEngine.Object.Destroy(_currentDepthMaterial);
            if (_currentTextMaterial != null)
                UnityEngine.Object.Destroy(_currentTextMaterial);
            if (_currentEnvironmentMaterial != null)
                UnityEngine.Object.Destroy(_currentEnvironmentMaterial);
            if (_currentSprite != null)
                UnityEngine.Object.Destroy(_currentSprite);
            if (_currentSpriteTexture != null)
                UnityEngine.Object.Destroy(_currentSpriteTexture);
            if (_currentDynamicFont != null)
                UnityEngine.Object.Destroy(_currentDynamicFont);
            _currentRoot = null;
            _currentDepthMaterial = null;
            _currentTextMaterial = null;
            _currentEnvironmentMaterial = null;
            _currentSprite = null;
            _currentSpriteTexture = null;
            _currentDynamicFont = null;
        }
    }
}
