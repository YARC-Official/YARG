Shader "Hidden/YARG/VenueAlphaFix"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include_with_pragmas "Assets/Art/Shaders/VenueShaders/VenuePP.hlsl"

        half4 FragAlphaFix(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float2 uvDistorted = YargVenueUV(uv);

            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                uvDistorted * _BlitScaleBias.xy + _BlitScaleBias.zw);

            // Applies the venue effects (posterize, scanlines, trails) and
            // forces alpha to 1.0 so the venue composites opaquely.
            return YargVenuePP(color.rgb, uvDistorted, uv);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "AlphaFix"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragAlphaFix
            ENDHLSL
        }
    }

    Fallback Off
}
