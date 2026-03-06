Shader "Posterize"
{
    Properties
    {
        _Steps ("Steps", Integer) = 5
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            FRAMEBUFFER_INPUT_X_HALF(0);
            int _Steps;

            float4 frag (Varyings input) : SV_Target
            {
                half4 col = LOAD_FRAMEBUFFER_X_INPUT(0, input.positionCS.xy);
                // posterize to n steps of color
                col = floor(col * _Steps) / _Steps;
                return col;
            }
            ENDHLSL
        }
    }
}
