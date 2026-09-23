Shader "HighwaysAlphaMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
            Cull Off
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
    }
}
