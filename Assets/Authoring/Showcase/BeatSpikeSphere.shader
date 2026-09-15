// Showcase: sphere that grows noise spikes on every beat, like a classic
// media-player visualizer ball. Displacement is done in the vertex stage.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/BeatSpikeSphere"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.35, 0.9, 1.0)
        _TipColor ("Spike Tip Color", Color) = (0.4, 0.9, 1.0, 1.0)
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

            fixed4 _BaseColor;
            fixed4 _TipColor;

            // Cheap deterministic pseudo-noise from a direction vector
            float SpikeNoise(float3 dir)
            {
                return 0.5 + 0.5 * sin(dir.x * 17.0) * cos(dir.y * 23.0) * sin(dir.z * 29.0);
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float spike : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                // Sharp attack, smooth decay over the beat
                float pulse = pow(saturate(1.0 - YargGameStateBeatPhase()), 5.0);
                float strong = pow(saturate(1.0 - YargGameStateMeasurePhase() * 2.0), 2.0);

                float n = SpikeNoise(normalize(v.normal));
                float spike = pulse * (n * (0.7 + 0.6 * strong));

                v.vertex.xyz += v.normal * spike * 0.45;

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.spike = saturate(spike * 1.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 col = lerp(_BaseColor.rgb, _TipColor.rgb, i.spike);
                col *= 0.6 + 1.2 * i.spike;
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
