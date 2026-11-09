Shader "PerfLab/HudIndirectDepth"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _VisibleHuds;
            struct Attributes { float4 positionOS : POSITION; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                float3 anchor = _VisibleHuds[input.instanceID].xyz;
                float3 right = normalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20));
                float3 up = normalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21));
                float3 world = anchor + right * (input.positionOS.x * 2.65) + up * (input.positionOS.y * 0.7);
                Varyings output;
                output.positionHCS = TransformWorldToHClip(world);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
