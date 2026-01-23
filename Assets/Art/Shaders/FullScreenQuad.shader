Shader "Custom/FullScreenQuad"
{
    Properties
    {
        _MainTex ("Main texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            // Only render to depth buffer
            ColorMask RGBA

            // We don't want this to be culled
            Cull Off

            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Make fog work
            #pragma multi_compile_fog

            sampler2D _MainTex;

            struct Attributes
            {
                float4 positionHCS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                float2  uv          : TEXCOORD0;
            };

            // Basically just forces the quad to be over the whole camera
            Varyings vert(Attributes input)
            {
                Varyings output;
                // Expects you're using the default Unity quad
                output.positionCS = float4(input.positionHCS.xy * 2.0, 0.0, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.positionCS.y *= -1;
                #endif

                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings IN) : SV_Target {
                return tex2D(_MainTex, IN.uv);
            }

            ENDCG
        }
    }
}
