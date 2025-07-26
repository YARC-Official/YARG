Shader "HighwayStencil"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderQueue" = "Transparent"}

        ColorMask 0      // Don't write to any color buffer
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off

        Pass
        {
            Name "HighwayStencil"

            Stencil
            {
                Ref 8
                Comp always
                Pass replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
   
 
            float4 frag (Varyings IN) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, IN.uv);

                if (color.a < 0.9)
                {
                    discard;
                }

                return color;

            }
            ENDHLSL
        }
    }
}
