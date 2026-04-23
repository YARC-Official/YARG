Shader "Hidden/YARG/VenueAlphaFix"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        half4 FragAlphaFix(Varyings input) : SV_Target
        {
            half4 color = FragBlit(input, sampler_LinearClamp);
            color.a = 1.0;
            return color;
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
