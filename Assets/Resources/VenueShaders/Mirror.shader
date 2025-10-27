Shader "Mirror"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _StartTime ("Start Time", Float) = 0
        _WipeLength ("Wipe Length", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local LEFT RIGHT CLOCK_CCW NONE
            #pragma vertex vert
            #pragma fragment frag
            // Include necessary headers for shader functions
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _StartTime;
            float _WipeLength;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float4 col;
                float2 uv = input.uv;

                float elapsedTime = _Time.y - _StartTime;
                float t = saturate(elapsedTime / _WipeLength);

                #if LEFT
                    float mirrorPoint = lerp(1.0, 0.0, t);

                    if (uv.x > mirrorPoint)
                    {
                        uv.x = 1 - uv.x;
                    }
                #elif RIGHT
                    float mirrorPoint = lerp(0.0, 0.5, t);

                    if (uv.x < mirrorPoint)
                    {
                        uv.x = 2 * mirrorPoint - uv.x;
                    }
                #elif CLOCK_CCW
                    float startAngle = 0.0;
                    float endAngle = 2 * 3.14159;
                    float currentAngle = lerp(startAngle, endAngle, t);

                    float2 centered = uv - float2(0.5, 0.5);
                    float pixelAngle = atan2(centered.y, centered.x);
                    if (pixelAngle < 0)
                    {
                        pixelAngle += 2 * 3.14159;
                    }

                    if (pixelAngle <= currentAngle)
                    {
                        uv.x = 1.0 - uv.x;
                    }
                #else
                    uv = input.uv;
                    if (uv.x < 0.5)
                    {
                        uv.x = 1 - uv.x;
                    }
                #endif

                // Sample the main texture
                col = tex2D(_MainTex, uv);

                return col;
            }
            ENDHLSL
        }
    }
}