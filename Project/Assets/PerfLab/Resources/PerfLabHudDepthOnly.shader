Shader "PerfLab/HudDepthOnly"
{
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
