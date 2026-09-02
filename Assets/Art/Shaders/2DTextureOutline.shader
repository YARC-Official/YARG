Shader "Custom/2DTextureOutline"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        // Outline Properties
        [HDR] _OutlineColor("Outline Color", Color) = (1,0,0,1)
        _OutlineWidth("Outline Width (Pixels)", Range(0.0, 16.0)) = 2.0
        _AlphaThreshold("Alpha Threshold", Range(0.01, 1.0)) = 0.1

        // Render State Control
        [HideInInspector] _Surface("__surface", Float) = 1.0
        [HideInInspector] _Blend("__mode", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _SrcBlend("__src", Float) = 5.0
        [HideInInspector] _DstBlend("__dst", Float) = 10.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 10.0
        [HideInInspector] _ZWrite("__zw", Float) = 0.0
        [HideInInspector] _ZTest("__zt", Float) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlitOutline"

            Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #if SHADER_API_METAL
            #pragma dynamic_branch _ _FOVEATED_RENDERING_NON_UNIFORM_RASTER
            #endif

            #include "Assets/Art/Shaders/YargParticlesUnlitInput.hlsl"

            CBUFFER_START(OutlineProperties)
                half4 _OutlineColor;
                float _OutlineWidth;
                half _AlphaThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;

                // Base sampling offset vector scaled by texture dimensions
                float2 radius = _BaseMap_TexelSize.xy * _OutlineWidth;
                half maxAlpha = 0.0;

                static const int SAMPLE_COUNT = 32;
                static const float ANGLE_STEP = 6.28318530718 / 32.0;

                [unroll]
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float angle = i * ANGLE_STEP;
                    float2 offset = float2(cos(angle), sin(angle)) * radius;
                    half sampleAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + offset).a;
                    maxAlpha = max(maxAlpha, sampleAlpha);
                }

                half innerMask = smoothstep(_AlphaThreshold - 0.05, _AlphaThreshold + 0.05, mainColor.a);

                half outlineMask = smoothstep(_AlphaThreshold - 0.05, _AlphaThreshold + 0.05, maxAlpha);

                half4 outlineCol = _OutlineColor;
                outlineCol.a *= outlineMask;

                // Blend base color over outline color, using innerMask as the blend factor
                half4 result = lerp(outlineCol, mainColor, innerMask);
                result.a = max(mainColor.a * innerMask, outlineCol.a);

                return result;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}