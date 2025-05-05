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
                float uv_x;
                if (_CurveFactor >= 0)
                    uv_x = IN.uv.x;
                else
                    uv_x = 0.5;
                    
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real original_depth = SampleSceneDepth(float2(uv_x, IN.uv.y));
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real original_depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(float2(uv_x, IN.uv.y)));
                #endif

                // Reconstruct the world space positions.
                float3 originalWorldPos = ComputeWorldSpacePosition(IN.uv, original_depth, UNITY_MATRIX_I_VP);

                float delta_x = abs(_WorldSpaceCameraPos.x - originalWorldPos.x);
                originalWorldPos.y += pow(delta_x, 2) * _CurveFactor * 0.1;

                float4 clipPos = mul(UNITY_MATRIX_VP, float4(originalWorldPos, 1.0));
                float2 UV = (clipPos.xy / clipPos.w) * 0.5 + 0.5;

                {
                    UV.y = 1 - UV.y;
                }
                
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif
                float sceneEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
                
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, UV);
                

                float rate = _FadeParams.y != _FadeParams.x ? 1.0 / (_FadeParams.y - _FadeParams.x) : 0.0;
                float alpha = smoothstep(0.0, 1.0, ((min(max(sceneEyeDepth, _FadeParams.x), _FadeParams.y)) - _FadeParams.x) * rate);
                color.a = color.a == 0.0 ? 0.0 : max(1.0 - _IsFading, min(color.a, 1.0 - alpha));

                return color;

            }
            ENDHLSL
        }
    }
}
