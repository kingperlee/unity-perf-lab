using UnityEngine;
using UnityEngine.Rendering;

namespace PerfLab.Samples.MassHud
{
    internal sealed class MassHudDamagePopupRenderer : MonoBehaviour
    {
        private const int FontSize = 64;
        private const float CharacterSize = 0.16f;
        private const int PoolCapacity = 256;
        private const int MaxDigits = 4;
        private const int MaxInstancesPerDraw = 1023;
        private const float SpawnRatePerSecond = 120f;
        private static readonly int GlyphUvBottomId = Shader.PropertyToID("_GlyphUvBottom");
        private static readonly int GlyphUvTopId = Shader.PropertyToID("_GlyphUvTop");
        private static readonly int[] PowersOfTen = { 1, 10, 100, 1000 };

        private struct Popup
        {
            public bool active;
            public int hudIndex;
            public int value;
            public float startTime;
            public float duration;
            public float lateralOffset;
        }

        private struct DigitGlyph
        {
            public float advance;
            public Vector2 center;
            public Vector2 size;
            public Vector4 uvBottom;
            public Vector4 uvTop;
        }

        private sealed class DrawBatch
        {
            public int count;
            public readonly Matrix4x4[] matrices = new Matrix4x4[MaxInstancesPerDraw];
            public readonly Vector4[] uvBottom = new Vector4[MaxInstancesPerDraw];
            public readonly Vector4[] uvTop = new Vector4[MaxInstancesPerDraw];
            public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }

        private Camera _camera;
        private Vector3[] _anchors;
        private Popup[] _pool;
        private DigitGlyph[] _digits;
        private DrawBatch[] _batches;
        private Mesh _quadMesh;
        private Material _material;
        private int _spawnCursor;
        private int _spawnSequence;
        private float _spawnAccumulator;
        private bool _initialized;

        public void Initialize(Camera targetCamera, Vector3[] anchors, Font font, Shader glyphShader)
        {
            _camera = targetCamera;
            _anchors = anchors;
            _pool = new Popup[PoolCapacity];
            font.RequestCharactersInTexture("0123456789", FontSize, FontStyle.Normal);
            _digits = BuildDigitGlyphs(font);
            int maxGlyphs = PoolCapacity * MaxDigits;
            _batches = new DrawBatch[Mathf.CeilToInt(maxGlyphs / (float)MaxInstancesPerDraw)];
            for (int i = 0; i < _batches.Length; i++)
                _batches[i] = new DrawBatch();
            _quadMesh = CreateQuadMesh();
            _material = new Material(glyphShader)
            {
                name = "Mass HUD Damage Popup Material",
                mainTexture = font.material.mainTexture,
                enableInstancing = true,
                renderQueue = 3004
            };
            _material.SetColor("_Color", new Color(1f, 0.58f, 0.12f, 1f));
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            SpawnByElapsedTime(Time.unscaledDeltaTime, Time.unscaledTime);
            BuildActiveGlyphs(Time.unscaledTime);
            DrawActiveGlyphs();
        }

        private void SpawnByElapsedTime(float deltaTime, float now)
        {
            _spawnAccumulator += deltaTime * SpawnRatePerSecond;
            int spawnCount = Mathf.Min(8, Mathf.FloorToInt(_spawnAccumulator));
            _spawnAccumulator -= spawnCount;
            for (int i = 0; i < spawnCount; i++)
                Spawn(now);
        }

        private void Spawn(float now)
        {
            int sequence = _spawnSequence;
            _spawnSequence = (_spawnSequence + 1) % 9990;
            _pool[_spawnCursor] = new Popup
            {
                active = true,
                hudIndex = sequence % _anchors.Length,
                value = 10 + sequence * 37 % 9990,
                startTime = now,
                duration = 1.15f,
                lateralOffset = ((sequence % 7) - 3) * 0.055f
            };
            _spawnCursor = (_spawnCursor + 1) % _pool.Length;
        }

        private void BuildActiveGlyphs(float now)
        {
            for (int i = 0; i < _batches.Length; i++)
                _batches[i].count = 0;

            Quaternion rotation = _camera.transform.rotation;
            int glyphIndex = 0;
            for (int popupIndex = 0; popupIndex < _pool.Length; popupIndex++)
            {
                Popup popup = _pool[popupIndex];
                if (!popup.active) continue;
                float normalizedAge = (now - popup.startTime) / popup.duration;
                if (normalizedAge >= 1f)
                {
                    popup.active = false;
                    _pool[popupIndex] = popup;
                    continue;
                }

                int digitCount = CountDigits(popup.value);
                float totalAdvance = 0f;
                for (int digitPosition = 0; digitPosition < digitCount; digitPosition++)
                    totalAdvance += _digits[GetDigit(popup.value, digitCount, digitPosition)].advance;

                float cursor = -totalAdvance * 0.5f;
                for (int digitPosition = 0; digitPosition < digitCount; digitPosition++)
                {
                    DigitGlyph digit = _digits[GetDigit(popup.value, digitCount, digitPosition)];
                    DrawBatch batch = _batches[glyphIndex / MaxInstancesPerDraw];
                    int destination = glyphIndex % MaxInstancesPerDraw;
                    Vector3 localPosition = new Vector3(
                        popup.lateralOffset + cursor + digit.center.x,
                        0.58f + normalizedAge * 1.1f + digit.center.y,
                        -0.035f);
                    batch.matrices[destination] = Matrix4x4.TRS(
                        _anchors[popup.hudIndex] + rotation * localPosition,
                        rotation,
                        new Vector3(digit.size.x, digit.size.y, 1f));
                    batch.uvBottom[destination] = digit.uvBottom;
                    batch.uvTop[destination] = digit.uvTop;
                    batch.count++;
                    glyphIndex++;
                    cursor += digit.advance;
                }
            }

            for (int i = 0; i < _batches.Length && _batches[i].count > 0; i++)
            {
                _batches[i].properties.SetVectorArray(GlyphUvBottomId, _batches[i].uvBottom);
                _batches[i].properties.SetVectorArray(GlyphUvTopId, _batches[i].uvTop);
            }
        }

        private void DrawActiveGlyphs()
        {
            for (int i = 0; i < _batches.Length; i++)
            {
                DrawBatch batch = _batches[i];
                if (batch.count == 0) break;
                Graphics.DrawMeshInstanced(
                    _quadMesh,
                    0,
                    _material,
                    batch.matrices,
                    batch.count,
                    batch.properties,
                    ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    _camera,
                    LightProbeUsage.Off);
            }
        }

        private static DigitGlyph[] BuildDigitGlyphs(Font font)
        {
            var result = new DigitGlyph[10];
            float unitsPerPixel = CharacterSize / FontSize;
            for (int i = 0; i < result.Length; i++)
            {
                CharacterInfo info;
                font.GetCharacterInfo((char)('0' + i), out info, FontSize, FontStyle.Normal);
                result[i] = new DigitGlyph
                {
                    advance = info.advance * unitsPerPixel,
                    center = new Vector2(
                        (info.minX + info.maxX) * 0.5f * unitsPerPixel,
                        (info.minY + info.maxY) * 0.5f * unitsPerPixel),
                    size = new Vector2(
                        (info.maxX - info.minX) * unitsPerPixel,
                        (info.maxY - info.minY) * unitsPerPixel),
                    uvBottom = new Vector4(
                        info.uvBottomLeft.x,
                        info.uvBottomLeft.y,
                        info.uvBottomRight.x,
                        info.uvBottomRight.y),
                    uvTop = new Vector4(
                        info.uvTopLeft.x,
                        info.uvTopLeft.y,
                        info.uvTopRight.x,
                        info.uvTopRight.y)
                };
            }
            return result;
        }

        private static int CountDigits(int value)
        {
            if (value >= 1000) return 4;
            if (value >= 100) return 3;
            if (value >= 10) return 2;
            return 1;
        }

        private static int GetDigit(int value, int digitCount, int position)
        {
            int divisor = PowersOfTen[digitCount - position - 1];
            return value / divisor % 10;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "Mass HUD Damage Glyph Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void OnDestroy()
        {
            _initialized = false;
            Destroy(_quadMesh);
            Destroy(_material);
        }
    }
}
