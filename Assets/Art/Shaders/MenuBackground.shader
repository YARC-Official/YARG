Shader "Unlit/MenuBackground"
{
    Properties
    {
        _Color_SideA      ("Color Side A",     Color) = (0.000, 0.859, 0.992, 1)
        _Color_SideB      ("Color Side B",     Color) = (0.929, 0.188, 0.125, 1)
        _Color_Background ("Color Background", Color) = (0.000, 0.043, 0.098, 1)

        _Point_Strength   ("Point Strength",   float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color_SideA;
            float4 _Color_SideB;
            float4 _Color_Background;
            float _Point_Strength;

            const float EPSILON = 1e-6;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            void iteration(inout float3 color, inout float amount, const float2 uv, const float2 pPos, const float4 pColor)
            {
                float dist = distance(uv, pPos) / _Point_Strength + EPSILON;
                dist = 1 - exp(-dist * dist * 10);
                const float strength = 1 - min(EPSILON, 1 * log(dist)) - 1;

                color = (pColor * strength + color * amount) / (amount + strength);
                amount += strength;
            }

            float2 circle(float radius, float speed, float offsetRadians)
            {
                return float2(
                    cos(_Time.y * speed + offsetRadians),
                    sin(_Time.y * speed + offsetRadians)) * radius;
            }

            float4 frag(v2f i) : SV_Target
            {
                const float2 uv = i.uv.xy - 0.5;

                // Get the points of the gradient
                const float2 sideAPoint = circle(0.75, 0.2, _SinTime.x);
                const float2 sideBPoint = circle(0.75, 0.2, 0);
                const float2 bgPoint    = circle(_CosTime.y * 0.2 + 0.2, 0.2, UNITY_HALF_PI);

                // Create gradient
                float3 color = float3(0, 0, 0);
                float amount = 0;
                iteration(color, amount, uv,  sideAPoint,      _Color_SideA);
                iteration(color, amount, uv, -sideBPoint,      _Color_SideB);
                iteration(color, amount, uv,  bgPoint, _Color_Background);
                iteration(color, amount, uv, -bgPoint, _Color_Background);
                iteration(color, amount, uv, float2(0, 0),     _Color_Background);

                return float4(color, 1.0);
            }

            ENDCG
        }
    }
}
