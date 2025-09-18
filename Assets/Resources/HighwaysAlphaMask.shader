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
            Blend One Zero   // no blending, overwrite red channel

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
                // Euclidean distance from camera to this fragment
                float3 camPos = YargWorldSpaceCameraPos(IN.positionWS);
                float dist = distance(camPos, IN.positionWS);
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
