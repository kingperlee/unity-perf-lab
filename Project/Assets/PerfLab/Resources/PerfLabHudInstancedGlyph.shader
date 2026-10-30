Shader "PerfLab/HudInstancedGlyph"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GlyphProperties)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GlyphUvBottom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GlyphUvTop)
            UNITY_INSTANCING_BUFFER_END(GlyphProperties)

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
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 uvBottom = UNITY_ACCESS_INSTANCED_PROP(GlyphProperties, _GlyphUvBottom);
                float4 uvTop = UNITY_ACCESS_INSTANCED_PROP(GlyphProperties, _GlyphUvTop);
                float2 bottom = lerp(uvBottom.xy, uvBottom.zw, input.uv.x);
                float2 top = lerp(uvTop.xy, uvTop.zw, input.uv.x);

                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = lerp(bottom, top, input.uv.y);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
