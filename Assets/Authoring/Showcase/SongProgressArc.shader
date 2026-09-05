// Showcase: thin torus arc that fills up with song progress. During the
// pre-song countdown it shows the countdown depleting instead. Render the
// torus flat (like a ring around something) or upright; the arc always
// starts at local +X and sweeps counter-clockwise when viewed from +Y.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/SongProgressArc"
{
    Properties
    {
        _TrackColor ("Unfilled Track Color", Color) = (0.15, 0.15, 0.18, 1.0)
        _FillColor ("Filled Arc Color", Color) = (1.0, 0.35, 0.1, 1.0)
        // Countdown display window in seconds (song start delay is 2s)
        _CountdownWindow ("Countdown Window (seconds)", Float) = 2.0
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

            fixed4 _TrackColor;
            fixed4 _FillColor;
            float _CountdownWindow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float angle01 : TEXCOORD0;   // 0 at +X, sweeping CCW seen from +Y
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float ang = atan2(v.vertex.z, v.vertex.x);
                o.angle01 = frac(ang / 6.2831853 + 1.0);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float countdown = YargGameStateCountdown();

                // During the countdown: arc shows remaining time depleting.
                // While playing: arc fills with song progress.
                float fill = countdown > 0.001
                    ? saturate(1.0 - countdown / max(_CountdownWindow, 0.001))
                    : saturate(YargGameStateSongProgress());

                // Soft edge on the arc head
                float head = smoothstep(fill - 0.004, fill + 0.004, i.angle01);

                fixed3 col = lerp(_FillColor.rgb * (1.6 + 0.4 * sin(_Time.y * 3.0)),
                                  _TrackColor.rgb, head);
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
