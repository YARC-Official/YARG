Shader "YARG/Gameplay/Vocals Track Tinted"
{
    Properties
    {
        [NoScaleOffset] _Texture2D ("Track Texture", 2D) = "white" {}
        _LaneColor1 ("HARM1 Color", Color) = (0, 0.8, 1, 1)
        _LaneColor2 ("HARM2 Color", Color) = (1, 0.52, 0, 1)
        _LaneColor3 ("HARM3 Color", Color) = (1, 0.86, 0, 1)
        _LaneCount ("Lane Count", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Geometry-100"
        }

        Pass
        {
            Name "VocalTrack"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Assets/Art/Shaders/ShaderGraph/Includes/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_Texture2D);
            SAMPLER(sampler_Texture2D);

            CBUFFER_START(UnityPerMaterial)
                half4 _LaneColor1;
                half4 _LaneColor2;
                half4 _LaneColor3;
                half _LaneCount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetVertexPositionInputs(input.positionOS.xyz).positionCS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_Texture2D, sampler_Texture2D, input.uv);

                half sourceMax = max(source.r, max(source.g, source.b));

                // Select by the authored band's vertical position instead of its
                // source hue. The soft glows blend orange and yellow together, so
                // hue-based selection can apply two profile colors to one lane.
                half4 laneColor = _LaneColor1;
                half laneMask = 0.0h;
                half warmLane = 0.0h;
                if (input.uv.y > 0.02h && input.uv.y < 0.20h)
                {
                    laneColor = _LaneColor1;
                    laneMask = 1.0h;
                }
                else if (input.uv.y > 0.855h && input.uv.y < 0.98h)
                {
                    laneColor = _LaneCount > 2.5h ? _LaneColor3 : _LaneColor2;
                    laneMask = _LaneCount > 1.5h ? 1.0h : 0.0h;
                    warmLane = 1.0h;
                }
                else if (_LaneCount > 2.5h && input.uv.y > 0.70h && input.uv.y < 0.848h)
                {
                    laneColor = _LaneColor2;
                    laneMask = 1.0h;
                    warmLane = 1.0h;
                }

                // Use the authored bands' channel separation to recolor even
                // their dim outer glow while leaving the track body and neutral
                // silver borders unchanged.
                half blueMask = smoothstep(0.0002h, 0.002h, max(source.g - source.r, 0.0h))
                    * laneMask;
                // The warm bands have a very dim red/orange outer glow. Use its
                // warm-channel separation so that fringe is recolored too, while
                // the neutral divider between HARM2 and HARM3 stays untouched.
                half warmMask = smoothstep(0.0002h, 0.002h, max(source.r - source.b, 0.0h))
                    * laneMask;
                half colorMask = lerp(blueMask, warmMask, warmLane);
                half3 tinted = laneColor.rgb * sourceMax;
                half3 color = lerp(source.rgb, tinted, colorMask);

                // Match the original graph's ten-percent screen-edge fade.
                float screenX = input.screenPos.x / input.screenPos.w;
                half edgeFade = smoothstep(0.0h, 0.1h, screenX)
                    * smoothstep(1.0h, 0.9h, screenX);

                return half4(color, saturate(source.a * 2.0h * edgeFade));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
