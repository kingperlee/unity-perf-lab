using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PerfLab.Editor
{
    [InitializeOnLoad]
    internal static class UrpProjectMigration
    {
        private const string SettingsFolder = "Assets/PerfLab/Settings";
        private const string PipelinePath = SettingsFolder + "/PerfLabURP.asset";
        private const string RendererPath = SettingsFolder + "/PerfLabUniversalRenderer.asset";

        static UrpProjectMigration()
        {
            EditorApplication.delayCall += ConfigureIfNeeded;
        }

        [MenuItem("PerfLab/Rendering/Configure URP")]
        private static void ConfigureIfNeeded()
        {
            EnsureFolder();
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                pipeline.name = "PerfLabURP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);

                ScriptableRendererData renderer = pipeline.LoadBuiltinRendererData();
                renderer.name = "PerfLabUniversalRenderer";
                string moveError = AssetDatabase.MoveAsset(
                    "Assets/UniversalRenderer.asset",
                    RendererPath);
                if (!string.IsNullOrEmpty(moveError))
                    throw new System.InvalidOperationException(moveError);
                EditorUtility.SetDirty(pipeline);
            }

            pipeline.useSRPBatcher = true;
            GraphicsSettings.renderPipelineAsset = pipeline;
            QualitySettings.renderPipeline = null;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "PerfLab URP configured. Pipeline: " + PipelinePath +
                ", SRP Batcher: " + pipeline.useSRPBatcher);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets/PerfLab", "Settings");
        }
    }
}
