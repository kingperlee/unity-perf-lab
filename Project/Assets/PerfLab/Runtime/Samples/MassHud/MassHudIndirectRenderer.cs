using UnityEngine;
using UnityEngine.Rendering;

namespace PerfLab.Samples.MassHud
{
    internal sealed class MassHudIndirectRenderer : MonoBehaviour
    {
        private static readonly int AllHudsId = Shader.PropertyToID("_AllHuds");
        private static readonly int VisibleHudsId = Shader.PropertyToID("_VisibleHuds");
        private static readonly int ViewProjectionId = Shader.PropertyToID("_ViewProjection");
        private static readonly int HudCountId = Shader.PropertyToID("_HudCount");

        private Camera _camera;
        private ComputeShader _cullShader;
        private int _kernel;
        private int _hudCount;
        private Mesh _quadMesh;
        private Material _depthMaterial;
        private Material _compositeMaterial;
        private ComputeBuffer _allHuds;
        private ComputeBuffer _visibleHuds;
        private ComputeBuffer _args;
        private Bounds _drawBounds;
        private bool _initialized;

        public void Initialize(
            Camera targetCamera,
            Vector3[] anchors,
            ComputeShader cullShader,
            Shader depthShader,
            Shader compositeShader)
        {
            _camera = targetCamera;
            _cullShader = cullShader;
            _kernel = cullShader.FindKernel("CullHuds");
            _hudCount = anchors.Length;
            _quadMesh = CreateQuadMesh();
            _depthMaterial = new Material(depthShader)
            {
                name = "Mass HUD Indirect Depth",
                enableInstancing = true
            };
            _compositeMaterial = new Material(compositeShader)
            {
                name = "Mass HUD Indirect Composite",
                enableInstancing = true,
                renderQueue = 3000
            };

            var source = new Vector4[anchors.Length];
            for (int i = 0; i < anchors.Length; i++)
                source[i] = new Vector4(anchors[i].x, anchors[i].y, anchors[i].z, i);
            _allHuds = new ComputeBuffer(anchors.Length, sizeof(float) * 4, ComputeBufferType.Structured);
            _allHuds.SetData(source);
            _visibleHuds = new ComputeBuffer(
                anchors.Length,
                sizeof(float) * 4,
                ComputeBufferType.Append);
            _args = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            _args.SetData(new[]
            {
                _quadMesh.GetIndexCount(0),
                0u,
                _quadMesh.GetIndexStart(0),
                _quadMesh.GetBaseVertex(0),
                0u
            });

            _cullShader.SetBuffer(_kernel, AllHudsId, _allHuds);
            _cullShader.SetBuffer(_kernel, VisibleHudsId, _visibleHuds);
            _cullShader.SetInt(HudCountId, _hudCount);
            _depthMaterial.SetBuffer(VisibleHudsId, _visibleHuds);
            _compositeMaterial.SetBuffer(VisibleHudsId, _visibleHuds);
            _drawBounds = new Bounds(new Vector3(0f, 25f, 40f), new Vector3(100f, 80f, 160f));
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            _visibleHuds.SetCounterValue(0);
            Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
            _cullShader.SetMatrix(ViewProjectionId, gpuProjection * _camera.worldToCameraMatrix);
            _cullShader.Dispatch(_kernel, Mathf.CeilToInt(_hudCount / 64f), 1, 1);
            ComputeBuffer.CopyCount(_visibleHuds, _args, sizeof(uint));

            Graphics.DrawMeshInstancedIndirect(
                _quadMesh, 0, _depthMaterial, _drawBounds, _args, 0, null,
                ShadowCastingMode.Off, false, gameObject.layer, _camera, LightProbeUsage.Off);
            Graphics.DrawMeshInstancedIndirect(
                _quadMesh, 0, _compositeMaterial, _drawBounds, _args, 0, null,
                ShadowCastingMode.Off, false, gameObject.layer, _camera, LightProbeUsage.Off);
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "Mass HUD Indirect Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
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
            _allHuds?.Release();
            _visibleHuds?.Release();
            _args?.Release();
            Destroy(_quadMesh);
            Destroy(_depthMaterial);
            Destroy(_compositeMaterial);
        }
    }
}
