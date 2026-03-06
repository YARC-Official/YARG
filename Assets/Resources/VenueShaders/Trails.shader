Shader "Trails"
{
    Properties
    {
        _Length ("Trail Length", Float) = 0.5
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        // Blend to get a trail effect
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            FRAMEBUFFER_INPUT_X_HALF(0);
            float _Length;

            float4 frag(Varyings input) : SV_Target
            {
                half4 col = LOAD_FRAMEBUFFER_X_INPUT(0, input.positionCS.xy);

                col.a = _Length;

                return col;
            }
            ENDHLSL
        }
    }
}
