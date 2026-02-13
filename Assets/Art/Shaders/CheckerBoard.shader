// Original Shader: https://www.shadertoy.com/view/ldBXz3
// License: CC BY-NC-SA 3.0 (Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported)
// https://creativecommons.org/licenses/by-nc-sa/3.0/

Shader "Custom/ShadertoyGridEffect"
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float grid(float2 p) 
            {
                float2 orient = normalize(float2(1.0, 3.0));
                float2 perp = float2(orient.y, -orient.x);
                float val1 = floor(dot(p, orient));
                float val2 = floor(dot(p, perp));
                float sum = val1 + val2;
                float g = sum - 2.0 * floor(sum / 2.0);
                return g;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 iResolution = float2(800.0, 600.0);
                float2 fragCoord = i.uv * iResolution;
                
                float iTime = _Time.y * 0.5;
                
                float2 p = fragCoord.xy / 50.0 + float2(-iTime, iTime);
                float2 q = (fragCoord.xy - (iResolution.xy / 2.0)) / iResolution.x / 1.5;
                
                float4 c = float4(grid(p), grid(p), grid(p), 1.0);
                
                if (q.x + 0.1 * q.y > 100.0) 
                {
                    return c;
                }
                else 
                {
                    float4 cc = float4(0.0, 0.0, 0.0, 0.0);
                    float total = 0.0;
                    
                    float radius = length(q) * 100.0;
                    float samp = 1.0;
                    
                    for (float t = -samp; t <= samp; t += 1.0) 
                    {
                        float percent = t / samp;
                        float weight = 1.0 - abs(percent);
                        float u = t / 100.0;
                        
                        float2 dir = float2(
                            frac(sin(537.3 * (u + 0.5))), 
                            frac(sin(523.7 * (u + 0.25)))
                        );
                        dir = normalize(dir) * 0.01;
                        
                        float skew = percent * radius;
                        
                        float4 samplev = float4(
                            grid(float2(0.03, 0.0) + p + dir * skew),
                            grid(radius * float2(0.005, 0.00) + p + dir * skew),
                            grid(radius * float2(0.007, 0.00) + p + dir * skew),
                            1.0
                        );
                        
                        cc += samplev * weight;
                        total += weight;
                    }
                    
                    float4 result = cc / total - length(q) * float4(1.0, 1.0, 1.0, 1.0) * 1.5;
                    return result;
                }
            }
            ENDCG
        }
    }
}