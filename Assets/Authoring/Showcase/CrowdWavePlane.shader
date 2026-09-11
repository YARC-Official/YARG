// Showcase: flag-like plane whose waves grow bigger, faster and choppier
// as crowd intensity rises. Mellow = lazy swell, intense = choppy surf.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/CrowdWavePlane"
{
    Properties
    {
        _Color ("Color", Color) = (0.95, 0.75, 0.3, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Cull Off

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
                float intensity = saturate(YargGameStateCrowdIntensity());

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Big lazy swell at low intensity + small choppy waves at high
                float swell  = sin(worldPos.x * 1.2 - _Time.y * lerp(0.8, 2.2, intensity));
                float ripple = sin(worldPos.x * 6.0 - worldPos.z * 4.0 - _Time.y * lerp(1.0, 7.0, intensity));
                float wave = swell * 0.35 + ripple * 0.25 * intensity;
                // Waves grow with intensity
                wave *= lerp(0.25, 1.0, intensity);

                v.vertex.z += wave;

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wave = wave / max(lerp(0.25, 1.0, intensity), 0.01);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float crest = saturate(i.wave * 0.5 + 0.5);
                fixed3 col = _Color.rgb * (0.5 + 0.8 * crest);
                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
