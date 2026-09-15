// Showcase: orb with a double-thump heartbeat that races and reddens as
// the fail meter drops. Heartbeat rate is independent of the song beat -
// it only follows the band's fail state.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/HeartbeatOrb"
{
    Properties
    {
        _SafeColor ("Safe Color", Color) = (0.25, 0.95, 0.45, 1.0)
        _DangerColor ("Danger Color", Color) = (1.0, 0.12, 0.1, 1.0)
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

            fixed4 _SafeColor;
            fixed4 _DangerColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float glow : TEXCOORD0;
            };

            // Double-thump heartbeat waveform: two bumps per cycle
            float Heartbeat(float t)
            {
                float t1 = frac(t);
                float bump1 = pow(saturate(1.0 - t1 * 4.0), 2.0) * step(t1, 0.25);
                float bump2 = pow(saturate(1.0 - frac(t1 - 0.35) * 4.0), 2.0) * step(0.35, t1) * step(t1, 0.6);
                return max(bump1, bump2 * 0.7);
            }

            v2f vert(appdata_base v)
            {
                // Danger drives both the rate and the strength of the pulse
                float danger = 1.0 - saturate(YargGameStateFailMeter());
                float rate = lerp(1.0, 2.5, danger);
                float beat = Heartbeat(_Time.y * rate);

                float scale = 1.0 + beat * (0.05 + 0.20 * danger);
                v.vertex.xyz *= scale;

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.glow = beat;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fail = saturate(YargGameStateFailMeter());
                fixed3 col = lerp(_DangerColor.rgb, _SafeColor.rgb, fail);
                col *= 0.55 + 0.9 * i.glow;
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
