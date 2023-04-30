Shader "Custom/SpriteHueShiftShader"
{
	Properties
	{
		_Tiling ("Tiling", float) = 1
		_MainTex ("Texture", 2D) = "white" {}

		_HueDiff ("HueDiff", range(-0.5, 0.5)) = 0
		_ApplyRate("Apply Rate", range(0, 1)) = 1
		_Auto ("Auto", int) = 0
		_AutoRate ("Auto Rate", float) = 1

	}
	SubShader
	{
		Tags { 
			"RenderType"="Transparent"
			"Queue"="Transparent"
		}
		LOD 100
		Blend SrcAlpha OneMinusSrcAlpha
//		ZTest Off

		Pass
		{

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
                fixed4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Tiling;

			float _HueDiff;
			int _Auto;
			float _AutoRate;
			float _ApplyRate;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed3 rgb2hsv(fixed3 rgb) {
				float4 k = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
   				float4 p = rgb.g < rgb.b ? float4(rgb.b, rgb.g, k.w, k.z) : float4(rgb.gb, k.xy);
				float4 q = rgb.r < p.x   ? float4(p.x, p.y, p.w, rgb.r) : float4(rgb.r, p.yzx);
				float d = q.x - min(q.w, q.y);
				float e = 1.0e-10;
				return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
				return rgb;
			}

			fixed3 hsv2rgb(fixed3 hsv) {
				float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				float3 p = abs(frac(hsv.xxx + k.xyz) * 6.0 - k.www);
				return hsv.z * lerp(k.xxx, clamp(p - k.xxx, 0.0, 1.0), hsv.y);
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
//				i.uv.x = fmod(i.uv.x * _Tiling - fmod(_Time.y, 1) * 2, 1);
//				i.uv.x = i.uv.x < 0 ? i.uv.x + 1 : i.uv.x;

				fixed4 col = tex2D(_MainTex, i.uv) * i.color;

//				fixed4 tint = fixed4(_Tint.xyz, 1);
//				tint = clamp(tint, 0.01, 1);
//				col = col / tint;
//				col = clamp(col, 0, 1);
				fixed3 hsv = rgb2hsv(col.xyz);

				fixed hueDiff = _HueDiff;

				if (_Auto > 0) hueDiff += fmod(_Time.y * _AutoRate, 1);

				hsv.x = fmod(hueDiff, 1);
				col.xyz = col.xyz * (1 - _ApplyRate) + hsv2rgb(hsv) * _ApplyRate;

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}





