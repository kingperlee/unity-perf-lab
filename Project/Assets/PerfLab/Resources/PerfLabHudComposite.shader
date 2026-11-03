Shader "PerfLab/HudComposite"
{
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(HudProperties)
                UNITY_DEFINE_INSTANCED_PROP(float, _Health01)
            UNITY_INSTANCING_BUFFER_END(HudProperties)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float health01 : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.health01 = UNITY_ACCESS_INSTANCED_PROP(HudProperties, _Health01);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                const float left = 0.03846;
                const float right = 0.96154;
                const float bottom = 0.10294;
                const float top = 0.36765;
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
