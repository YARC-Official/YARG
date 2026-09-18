// Standalone Venue post-processing pass. Runs after URP post-processing on the
// venue camera so the patched AA_UberPost shader is no longer required for venue effects.
// Output is written to the persistent trails texture, with alpha forced to 1.0 to
// prevent transparency artifacts when the venue renders without post-processing.
Shader "Hidden/YARG/VenuePP"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Assets/Art/Shaders/VenueShaders/VenuePP.hlsl"

        half4 FragVenuePP(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            // Mirror wipe warps the sampling UV. Posterize and scanlines are
            // applied in warped space to match the old UberPost integration
            // (which fed the mirrored uvDistorted into YargScanlines).
            float2 uvDistorted = YargVenueUV(uv);

            half3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvDistorted).rgb;

            return YargVenuePP(color, uvDistorted, uv);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "VenuePP"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                // VenuePP.hlsl declares these with #pragma multi_compile_local, which is
                // not processed via plain #include; declare them here instead. Controlled
                // via global Shader.EnableKeyword from VenueCameraRenderer.
                #pragma multi_compile_local _ YARG_MIRROR_LEFT YARG_MIRROR_RIGHT YARG_MIRROR_CLOCK_CCW YARG_MIRROR_NONE

                #pragma vertex Vert
                #pragma fragment FragVenuePP
            ENDHLSL
        }
    }

    Fallback Off
}
