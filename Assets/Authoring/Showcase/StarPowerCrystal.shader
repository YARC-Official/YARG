// Showcase: crystal that fills with light as star power charges, and
// ignites with a fresnel rim while star power is active. Fill line is
// computed from local height, so stretch the object vertically freely.
// Part of the game state texture showcase (see Assets/Art/Shaders/gamestate.hlsl).

Shader "YARG/Showcase/StarPowerCrystal"
{
    Properties
    {
        _EmptyColor ("Empty Color", Color) = (0.05, 0.07, 0.12, 1.0)
        _ChargeColor ("Charge Color", Color) = (0.9, 0.75, 0.2, 1.0)
        _ActiveColor ("Active Color", Color) = (0.25, 0.6, 1.0, 1.0)
        _Height ("Crystal Height (local units)", Float) = 2.0
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

            fixed4 _EmptyColor;
            fixed4 _ChargeColor;
            fixed4 _ActiveColor;
            float _Height;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float height01 : TEXCOORD0;   // 0 at bottom, 1 at top
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.height01 = saturate(v.vertex.y / max(_Height, 0.001) + 0.5);
                o.normal = v.normal;
                o.viewDir = ObjSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float charge = saturate(YargGameStateStarPowerCharge());
                float active = saturate(YargGameStateStarPowerActive());

                // Lit portion above the fill line, soft edge
                float fill = smoothstep(i.height01 - 0.04, i.height01 + 0.04, charge);

                fixed3 col = lerp(_EmptyColor.rgb, _ChargeColor.rgb * (0.8 + 0.4 * fill), fill);

                // Fresnel rim while active
                float fresnel = pow(saturate(1.0 - dot(normalize(i.normal), normalize(i.viewDir))), 2.0);
                col += _ActiveColor.rgb * fresnel * active;

                // Slow energy scroll while active
                col += _ActiveColor.rgb * active * 0.15 *
                    (0.5 + 0.5 * sin(i.height01 * 20.0 - _Time.y * 6.0));

                return fixed4(col, 1.0);
            }

            ENDCG
        }
    }
}
