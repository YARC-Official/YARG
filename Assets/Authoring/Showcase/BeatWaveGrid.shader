// Showcase: apply to any number of cubes laid out in a line or grid - they
// bob up and down as a traveling wave, synced so one full wave passes per
// beat. Wave phase is derived from world position, so no per-object setup
// is needed; just space the cubes out.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/BeatWaveGrid"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 0.6, 0.15, 1.0)
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

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float wave : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // One full wave cycle per beat; phase offset by world X so
                // neighboring cubes form a traveling wave
                float phase = YargGameStateBeatPhase() + worldPos.x * 0.18;
                float wave = sin(phase * 6.2831853);

                v.vertex.y += wave * 0.25;

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wave = wave;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Crests glow brighter
                float crest = saturate(i.wave * 0.5 + 0.5);
                fixed3 col = _Color.rgb * (0.45 + 0.9 * crest);
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
