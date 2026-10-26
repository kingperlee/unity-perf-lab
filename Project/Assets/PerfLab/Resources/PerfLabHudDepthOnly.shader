Shader "PerfLab/HudDepthOnly"
{
    SubShader
    {
        Tags
        {
            "Queue"="Geometry+10"
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
