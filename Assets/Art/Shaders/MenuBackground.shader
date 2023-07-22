Shader "Unlit/MenuBackground"
{
    Properties
    {
        _Color_SideA      ("Color Side A",     Color) = (0.000, 0.859, 0.992, 1)
        _Color_SideB      ("Color Side B",     Color) = (0.929, 0.188, 0.125, 1)
        _Color_Background ("Color Background", Color) = (0.000, 0.043, 0.098, 1)

        _Point_Strength   ("Point Strength", float) = 1.5

        [NoScaleOffset] _NoiseTex ("Dither Texture", 2D) = "grey" {}
        _NoiseRange ("Noise Range", float) = 0.1
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

            sampler2D _NoiseTex;
            float4 _NoiseTex_TexelSize;
            float _NoiseRange;

            const float EPSILON = 1e-6;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
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
                float2 uv = i.uv.xy - 0.5;

                // Calculate pixel accurate UVs for the noise texture
                // screen pos * screen resolution * (1 / noise texture resolution)
                const float2 noiseUV = i.screenPos * _ScreenParams.xy * _NoiseTex_TexelSize.xy;
                const float3 noise = tex2D(_NoiseTex, noiseUV);

                // Scale noise to be in a range
                const float3 dither = noise * _NoiseRange - _NoiseRange / 2.0;

                // Add the dither
                uv += dither.xy;

                // Get the points of the gradient
                const float2 sideAPoint = circle(0.75, 0.2, _SinTime.x);
                const float2 sideBPoint = circle(0.75, 0.2, 0);
                const float2 bgPoint    = circle(_CosTime.y * 0.2 + 0.2, 0.2, UNITY_HALF_PI);

                // Create gradient
                float3 color = float3(0, 0, 0);
                float amount = 0;
                iteration(color, amount, uv,  sideAPoint, _Color_SideA);
                iteration(color, amount, uv, -sideBPoint, _Color_SideB);
                iteration(color, amount, uv,  bgPoint,    _Color_Background);
                iteration(color, amount, uv, -bgPoint,    _Color_Background);

                // This middle point makes things overall darker and more spaced out,
                // however I don't really like the look of it.
                // iteration(color, amount, uv, float2(0, 0), _Color_Background);

                float4 frag = float4(color, 1.0);

                return frag;
            }

            ENDCG
        }
    }
}
