Shader "Custom/Funhouse/TallMirror"
{
    Properties
    {
        _MainTex ("Reflection", 2D) = "black" {}
        _VerticalStretch ("Vertical Stretch", Range(1.0, 3.0)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _VerticalStretch;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Move UV coordinates so that 0,0 is the
                // centre of the mirror.
                float2 uv = i.uv - 0.5;

                // Compress the area we sample from vertically.
                // This causes the image to appear stretched.
                uv.y /= _VerticalStretch;

                // Move back into normal UV space.
                uv += 0.5;

                // Don't display outside the texture.
                if (uv.x < 0 || uv.x > 1 ||
                    uv.y < 0 || uv.y > 1)
                {
                    return fixed4(0, 0, 0, 1);
                }

                return tex2D(_MainTex, uv);
            }

            ENDCG
        }
    }
}