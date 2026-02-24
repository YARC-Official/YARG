// Original Shader: https://www.shadertoy.com/view/3dVBDh
// License: CC BY-NC-SA 3.0 (Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported)
// https://creativecommons.org/licenses/by-nc-sa/3.0/

Shader "Custom/RotatingSquares"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            
            #define PI 3.14159265358979323846

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 rotate2D(float2 st, float angle)
            {
                st -= 0.5;
                float2x2 rotMatrix = float2x2(cos(angle), -sin(angle),
                                               sin(angle), cos(angle));
                st = mul(rotMatrix, st);
                st += 0.5;
                return st;
            }

            float2 tile(float2 st, float zoom, float rotD)
            {
                st *= zoom;
                if(rotD == 1.0) 
                {
                    st.x += 0.5;
                    st.y += 0.5;
                }
                return frac(st);
            }

            float square(float2 st, float2 side)
            {
                float2 border = float2(0.5, 0.5) - side * 0.5;
                float2 pq = smoothstep(border, border + 0.01, st);
                pq *= smoothstep(border, border + 0.01, float2(1.0, 1.0) - st);
                return pq.x * pq.y;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 iResolution = float2(800.0, 600.0);
                float2 fragCoord = i.uv * iResolution;
                float iTime = _Time.y;
                
                float2 uv = fragCoord.xy / iResolution.y;
                float color;
                float Nsquares = 5.0;
                float rotDirection = 0.0;
                float warpedTime = iTime * 0.5 + uv.x + uv.y * 0.5;
                
                rotDirection = step(0.0, sin(warpedTime * 2.0));
                
                uv = tile(uv, Nsquares, rotDirection);
                uv = rotate2D(uv, PI / 4.0 - warpedTime);
                
                if (rotDirection == 1.0)
                    color = 1.0 - square(uv, float2(0.71, 0.71));
                else 
                    color = square(uv, float2(0.72, 0.72));
                
                return float4(color, color, color, 1.0);
            }
            ENDCG
        }
    }
}