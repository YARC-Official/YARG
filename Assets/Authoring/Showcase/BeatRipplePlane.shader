// Showcase: floor plane with concentric ripples that emanate outward on
// every beat; a brighter wave rolls out on the downbeat of each measure.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/BeatRipplePlane"
{
    Properties
    {
        _Color ("Ripple Color", Color) = (0.2, 0.8, 1.0, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/Art/Shaders/gamestate.hlsl"

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float dist : TEXCOORD0;
                float brightness : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                // Ripple phase travels outward as the beat progresses,
                // so a new ring starts at the center every beat
                float beatPhase = YargGameStateBeatPhase();
                float strong = pow(saturate(1.0 - YargGameStateMeasurePhase() * 2.0), 2.0);

                float dist = length(v.vertex.xz);
                float wave = sin((dist * 1.5 - beatPhase) * 6.2831853);
                // Fade ripples out with distance from center
                float falloff = exp(-dist * 0.35);

                v.vertex.y += wave * 0.12 * falloff * (1.0 + strong);

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dist = dist;
                o.brightness = saturate(wave * 0.5 + 0.5) * falloff * (0.7 + strong);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float alpha = smoothstep(0.35, 1.0, i.brightness) * saturate(1.0 - i.dist * 0.12);
                return fixed4(_Color.rgb * i.brightness, alpha);
            }

            ENDCG
        }
    }
}
