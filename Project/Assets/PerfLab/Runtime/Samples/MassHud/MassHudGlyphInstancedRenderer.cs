using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerfLab.Samples.MassHud
{
    internal sealed class MassHudGlyphInstancedRenderer : MonoBehaviour
    {
        private const int FontSize = 64;
        private const float CharacterSize = 0.12f;
        private const int MaxInstancesPerDraw = 1023;
        private static readonly int GlyphUvBottomId = Shader.PropertyToID("_GlyphUvBottom");
        private static readonly int GlyphUvTopId = Shader.PropertyToID("_GlyphUvTop");

        private struct GlyphInstance
        {
            public int hudIndex;
            public Vector3 anchor;
            public Vector2 localCenter;
            public Vector2 size;
            public Vector4 uvBottom;
            public Vector4 uvTop;
        }

        private sealed class GlyphBatch
        {
            public int count;
            public Matrix4x4[] matrices;
            public Vector4[] uvBottom;
            public Vector4[] uvTop;
            public MaterialPropertyBlock properties;
        }

        private Camera _camera;
        private GlyphInstance[] _glyphs;
        private GlyphBatch[] _batches;
        private Material _material;
        private Mesh _quadMesh;
        private Quaternion _lastCameraRotation;
        private bool _hasCameraRotation;
        private MassHudVisibilityCuller _visibilityCuller;
        private int _lastVisibilityVersion = -1;
        private bool _initialized;

        public void Initialize(
            Camera targetCamera,
            Vector3[] anchors,
            Font font,
            Shader glyphShader,
            MassHudVisibilityCuller visibilityCuller)
        {
            _camera = targetCamera;
            _visibilityCuller = visibilityCuller;
            string[] names = BuildNames(anchors.Length);
            PrewarmFont(font, names);
            font.material.mainTexture.filterMode = FilterMode.Bilinear;
            _glyphs = BuildGlyphs(font, anchors, names);
            _batches = BuildBatches(_glyphs);
            _quadMesh = CreateQuadMesh();
            _material = new Material(glyphShader)
            {
                name = "Mass HUD Instanced Glyph Material",
                mainTexture = font.material.mainTexture,
                enableInstancing = true,
                renderQueue = 3003
            };
            _material.SetColor("_Color", Color.white);
            UpdateMatrices();
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            UpdateMatrices();
            DrawGlyphs();
        }

        private void UpdateMatrices()
        {
            Quaternion rotation = _camera.transform.rotation;
            int visibilityVersion = _visibilityCuller != null ? _visibilityCuller.Version : 0;
            if (_hasCameraRotation &&
                visibilityVersion == _lastVisibilityVersion &&
                Mathf.Abs(Quaternion.Dot(rotation, _lastCameraRotation)) > 0.999999f)
                return;

            _lastCameraRotation = rotation;
            _lastVisibilityVersion = visibilityVersion;
            _hasCameraRotation = true;
            for (int i = 0; i < _batches.Length; i++)
                _batches[i].count = 0;

            int visibleGlyphIndex = 0;
            for (int glyphIndex = 0; glyphIndex < _glyphs.Length; glyphIndex++)
            {
                GlyphInstance glyph = _glyphs[glyphIndex];
                if (_visibilityCuller != null && !_visibilityCuller.IsVisible(glyph.hudIndex))
                    continue;

                GlyphBatch batch = _batches[visibleGlyphIndex / MaxInstancesPerDraw];
                int destination = visibleGlyphIndex % MaxInstancesPerDraw;
                Vector3 localPosition = new Vector3(glyph.localCenter.x, glyph.localCenter.y, -0.025f);
                batch.matrices[destination] = Matrix4x4.TRS(
                    glyph.anchor + rotation * localPosition,
                    rotation,
                    new Vector3(glyph.size.x, glyph.size.y, 1f));
                batch.uvBottom[destination] = glyph.uvBottom;
                batch.uvTop[destination] = glyph.uvTop;
                batch.count++;
                visibleGlyphIndex++;
            }

            for (int i = 0; i < _batches.Length && _batches[i].count > 0; i++)
            {
                _batches[i].properties.SetVectorArray(GlyphUvBottomId, _batches[i].uvBottom);
                _batches[i].properties.SetVectorArray(GlyphUvTopId, _batches[i].uvTop);
            }
        }

        private void DrawGlyphs()
        {
            for (int i = 0; i < _batches.Length; i++)
            {
                GlyphBatch batch = _batches[i];
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

        private static GlyphBatch[] BuildBatches(GlyphInstance[] glyphs)
        {
            int batchCount = Mathf.CeilToInt(glyphs.Length / (float)MaxInstancesPerDraw);
            var batches = new GlyphBatch[batchCount];
            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                var properties = new MaterialPropertyBlock();
                batches[batchIndex] = new GlyphBatch
                {
                    matrices = new Matrix4x4[MaxInstancesPerDraw],
                    uvBottom = new Vector4[MaxInstancesPerDraw],
                    uvTop = new Vector4[MaxInstancesPerDraw],
                    properties = properties
                };
            }
            return batches;
        }

        private static string[] BuildNames(int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                int level = 1 + i % 99;
                switch (i % 5)
                {
                    case 0:
                        names[i] = "Player " + i + "  Lv." + level;
                        break;
                    case 1:
                        names[i] = "玩家" + i + "  等级" + level;
                        break;
                    case 2:
                        names[i] = "Игрок " + i + "  Ур." + level;
                        break;
                    case 3:
                        names[i] = "プレイヤー" + i + "  Lv." + level;
                        break;
                    default:
                        names[i] = "플레이어 " + i + "  Lv." + level;
                        break;
                }
            }
            return names;
        }

        private static void PrewarmFont(Font font, string[] names)
        {
            var unique = new HashSet<char> { '?' };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                for (int c = 0; c < name.Length; c++)
                    unique.Add(name[c]);
            }

            var characters = new StringBuilder(unique.Count);
            foreach (char character in unique)
                characters.Append(character);
            font.RequestCharactersInTexture(characters.ToString(), FontSize, FontStyle.Normal);
        }

        private static GlyphInstance[] BuildGlyphs(Font font, Vector3[] anchors, string[] names)
        {
            var glyphs = new List<GlyphInstance>(names.Length * 12);
            float unitsPerPixel = CharacterSize / FontSize;

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                float totalAdvance = 0f;
                for (int c = 0; c < name.Length; c++)
                {
                    CharacterInfo info = GetCharacterInfo(font, name[c]);
                    totalAdvance += info.advance;
                }

                float cursor = -totalAdvance * 0.5f;
                for (int c = 0; c < name.Length; c++)
                {
                    CharacterInfo info = GetCharacterInfo(font, name[c]);
                    int width = info.maxX - info.minX;
                    int height = info.maxY - info.minY;
                    if (width > 0 && height > 0)
                    {
                        glyphs.Add(new GlyphInstance
                        {
                            hudIndex = i,
                            anchor = anchors[i],
                            localCenter = new Vector2(
                                (cursor + (info.minX + info.maxX) * 0.5f) * unitsPerPixel,
                                0.14f + (info.minY + info.maxY) * 0.5f * unitsPerPixel),
                            size = new Vector2(width * unitsPerPixel, height * unitsPerPixel),
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
                        });
                    }
                    cursor += info.advance;
                }
            }

            return glyphs.ToArray();
        }

        private static CharacterInfo GetCharacterInfo(Font font, char character)
        {
            CharacterInfo info;
            if (font.GetCharacterInfo(character, out info, FontSize, FontStyle.Normal))
                return info;
            if (font.GetCharacterInfo('?', out info, FontSize, FontStyle.Normal))
                return info;
            return default(CharacterInfo);
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "Mass HUD Instanced Glyph Quad" };
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
            _initialized = false;
            Destroy(_quadMesh);
            Destroy(_material);
        }
    }
}
