// Showcase: torus that pulses on every beat and flashes on the downbeat.
// Displacement happens entirely in the vertex stage; tint follows the fail meter.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/BeatPulseTorus"
{
    Properties
    {
        _LowColor ("Low Fail Color", Color) = (1.0, 0.15, 0.1, 1.0)
        _HighColor ("High Fail Color", Color) = (0.2, 1.0, 0.4, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/Art/Shaders/gamestate.hlsl"

            fixed4 _LowColor;
            fixed4 _HighColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float intensity : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                // Sharp spike on each beat, decaying smoothly
                float pulse = pow(saturate(1.0 - YargGameStateBeatPhase()), 4.0);
                // Extra kick on the downbeat of each measure
                float strong = pow(saturate(1.0 - YargGameStateMeasurePhase() * 2.0), 2.0);

                v.vertex.xyz += v.normal * (pulse * (0.10 + 0.18 * strong));

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.intensity = pulse * (0.6 + 0.8 * strong);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fail = saturate(YargGameStateFailMeter());
                fixed3 baseCol = lerp(_LowColor.rgb, _HighColor.rgb, fail);

                fixed3 col = baseCol * (0.25 + i.intensity);
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
