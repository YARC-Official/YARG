Shader "HighwaysAlphaMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }

        // Pass 0: renders fade coverage into the mask texture's R channel.
        // Max-blended with no depth testing, so any rendered geometry can only
        // raise the mask. This is what lets the vocal track (which outputs a
        // full alpha of 1) override the highway fade instead of the highway
        // fade cutting holes into the vocal track.
        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask R
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Art/Shaders/highways.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = YargTransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                int index = WorldPosToIndex(IN.positionWS);
                float fadeStartPos = _YargFadeParams[index * 2];
                float fadeEndPos   = _YargFadeParams[index * 2 + 1];
                // public const float STRIKE_LINE_POS       = -2f;
                // the above in TrackPlayer needs to be kept in sync
                // for the fade to stay the same
                // ie the `dist` below relies on highways placed
                // at z = -2 and aligned with Z axis
                float dist = IN.positionWS.z;
                float alpha = 0.0;

                if (dist < fadeStartPos)
                {
                    alpha = 1.0;
                }
                else if (dist > fadeEndPos)
                {
                    alpha = 0.0;
                }
                else
                {
                    float rate = 1.0 / (fadeEndPos - fadeStartPos);
                    float fadeValue = (dist - fadeStartPos) * rate;
                    alpha = 1.0 - smoothstep(0.0, 1.0, fadeValue);
                }

                // Only write into R channel, others zero
                return half4(alpha, 0, 0, 0);
            }
            ENDHLSL
        }

        // Pass 1: full-screen pass that min-blends the mask into the alpha channel
        // of the highways output texture. By the time this runs, post-processing
        // has already written the final color and the scene alpha, so the result
        // is finalAlpha = min(sceneAlpha, mask), matching what the old UberPP
        // fork computed.
        Pass
        {
            Name "ApplyFadeAlphaMask"

            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask A
            Blend One One
            BlendOp Min

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragApplyAlphaMask
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragApplyAlphaMask(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    input.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw).r;

                // Only the alpha channel is blended (ColorMask A + BlendOp Min)
                return half4(0, 0, 0, mask);
            }
            ENDHLSL
        }
    }
}
