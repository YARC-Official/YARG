Shader "Mirror"
{
    Properties
    {
        _StartTime ("Start Time", Float) = 0
        _WipeLength ("Wipe Length", Float) = 0.5
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local LEFT RIGHT CLOCK_CCW NONE
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            FRAMEBUFFER_INPUT_X_HALF(0);
            float _StartTime;
            float _WipeLength;

            float4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float elapsedTime = _Time.y - _StartTime;
                float t = saturate(elapsedTime / _WipeLength);

                #if LEFT
                    float mirrorPoint = lerp(1.0, 0.0, t);

                    if (uv.x > mirrorPoint)
                    {
                        uv.x = 1 - uv.x;
                    }
                #elif RIGHT
                    float mirrorPoint = lerp(0.0, 0.5, t);

                    if (uv.x < mirrorPoint)
                    {
                        uv.x = 2 * mirrorPoint - uv.x;
                    }
                #elif CLOCK_CCW
                    float startAngle = 0.0;
                    float endAngle = 2 * 3.14159;
                    float currentAngle = lerp(startAngle, endAngle, t);

                    float2 centered = uv - float2(0.5, 0.5);
                    float pixelAngle = atan2(centered.y, centered.x);
                    if (pixelAngle < 0)
                    {
                        pixelAngle += 2 * 3.14159;
                    }

                    if (pixelAngle <= currentAngle)
                    {
                        uv.x = 1.0 - uv.x;
                    }
                #else
                    uv = input.texcoord;
                    if (uv.x < 0.5)
                    {
                        uv.x = 1 - uv.x;
                    }
                #endif

                float2 samplePos = uv * _ScreenParams.xy;
                half4 col = LOAD_FRAMEBUFFER_X_INPUT(0, samplePos);

                return col;
            }
            ENDHLSL
        }
    }
}
