Shader "HighwayBlit"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderQueue" = "Transparent"}
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "HighwayBlitPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionHCS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Note: The pass is setup with a mesh already in clip
                // space, that's why, it's enough to just output vertex
                // positions
                output.positionCS = float4(input.positionHCS.xyz, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.positionCS.y *= -1;
                #endif

                output.uv = input.uv;
                return output;
            }

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
   
            float2 _FadeParams;
            float _CurveFactor;
            float _IsFading;

            float4 frag (Varyings IN) : SV_Target
            {
                // Apply curving
                IN.uv.y += pow(abs(IN.uv.x - 0.5), 2) * _CurveFactor;
                
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, IN.uv);
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(IN.uv);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(IN.uv));
                #endif

                float sceneEyeDepth = LinearEyeDepth(depth, _ZBufferParams);

                float rate = _FadeParams.y != _FadeParams.x ? 1.0 / (_FadeParams.y - _FadeParams.x) : 0.0;
                float alpha = smoothstep(0.0, 1.0, ((min(max(sceneEyeDepth, _FadeParams.x), _FadeParams.y)) - _FadeParams.x) * rate);
                color.a = color.a == 0.0 ? 0.0 : max(1.0 - _IsFading, min(color.a, 1.0 - alpha));

                return color;

            }
            ENDHLSL
        }
    }
}
