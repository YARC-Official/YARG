Shader "Scanlines"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanlineColor ("Scanline Color", Color) = (0,0,0,0)
        _ScanlineSize ("Scanline Size", Float) = 180
        _ScanlineIntensity ("Scanline Intensity", Float) = 0.5
        _EasingPower ("Easing Power", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100

//        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
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
            float4 _ScanlineColor;
            float _ScanlineSize;
            float _ScanlineIntensity;
            float _EasingPower;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float ExpInOut(float t)
            {
                t = 2.0 * t - 1.0;

                float sign = (t < 0.0) ? -1.0 : 1.0;
                t = sign * (1.0 - pow(1.0 - abs(t), _EasingPower));

                return 0.5 * (t + 1.0);
            }

            float4 ColorBlend(float4 original, float4 scanline, float t)
            {
                // Apply exponential in-out easing to the blend factor
                float easedT = ExpInOut(t) * _ScanlineIntensity;

                float brightnessBoost = 1.0 + ((1.5 - 1.0) * (1.0 - easedT));

                float4 brightened = original * brightnessBoost;

                brightened = min(brightened, 1.0);

                // Custom blending formula - you can modify this as needed
                // This version creates a more intense effect in the midtones
                float4 result;
                result.r = brightened.r * (1.0 - easedT) + scanline.r * easedT;
                result.g = brightened.g * (1.0 - easedT) + scanline.g * easedT;
                result.b = brightened.b * (1.0 - easedT) + scanline.b * easedT;
                // This effect always needs to produce an output alpha of 1.0
                result.a = 1.0; // original.a;  // * (1.0 - easedT) + scanline.a * easedT;

                return result;
            }


            float4 frag (Varyings input) : SV_Target
            {
                // Sample the main texture
                float4 col = tex2D(_MainTex, input.uv);

                // Calculate scanline effect
                float scanline = frac(input.uv.y * _ScanlineSize);

                // Apply scanline color
                col = ColorBlend(col, _ScanlineColor, scanline);

                return col;
            }
            ENDHLSL
        }
    }
}
