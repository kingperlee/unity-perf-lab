Shader "PerfLab/HudIndirectComposite"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _VisibleHuds;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float health01 : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                float4 hud = _VisibleHuds[input.instanceID];
                float3 right = normalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20));
                float3 up = normalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21));
                float3 forward = normalize(float3(UNITY_MATRIX_I_V._m02, UNITY_MATRIX_I_V._m12, UNITY_MATRIX_I_V._m22));
                float3 world = hud.xyz + right * (input.positionOS.x * 2.6) +
                               up * (input.positionOS.y * 0.68) - forward * 0.01;
                Varyings output;
                output.positionHCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.health01 = 0.5 + 0.5 * sin(_Time.y * 1.7 + hud.w * 0.173);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                const float left = 0.03846, right = 0.96154;
                const float bottom = 0.10294, top = 0.36765;
                float inBar = step(left, input.uv.x) * step(input.uv.x, right) *
                              step(bottom, input.uv.y) * step(input.uv.y, top);
                float barU = saturate((input.uv.x - left) / (right - left));
                half4 background = half4(0.03, 0.03, 0.03, 0.92);
                half4 healthBack = half4(0.18, 0.04, 0.04, 1.0);
                half4 healthFill = half4(0.15, 0.9, 0.25, 1.0);
                half4 bar = lerp(healthBack, healthFill, step(barU, input.health01));
                return lerp(background, bar, inBar);
            }
            ENDHLSL
        }
    }
}
