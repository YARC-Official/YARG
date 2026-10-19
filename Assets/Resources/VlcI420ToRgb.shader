// Converts VlcCpuMediaPlayer's I420 planes to RGB as it draws them into its output texture
// (Graphics.DrawTexture). _MainTex is the luma plane; _UTex and _VTex are the half-size chroma
// planes, sampled at the same UV. libVLC's vmem output carries no colorimetry, so this assumes
// BT.709 limited range, which is what YouTube-sourced videos use at every size. Outputs the
// display-encoded values unchanged, as vlc-unity's own output textures hold them.
Shader "Hidden/YARG/VlcI420ToRgb"
{
    Properties
    {
        _MainTex ("Y", 2D) = "black" {}
        _UTex ("U", 2D) = "gray" {}
        _VTex ("V", 2D) = "gray" {}
    }

    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _UTex;
            sampler2D _VTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float y = (tex2D(_MainTex, i.uv).r - 16.0 / 255.0) * (255.0 / 219.0);
                float u = (tex2D(_UTex, i.uv).r - 128.0 / 255.0) * (255.0 / 224.0);
                float v = (tex2D(_VTex, i.uv).r - 128.0 / 255.0) * (255.0 / 224.0);

                float3 rgb = float3(
                    y + 1.5748 * v,
                    y - 0.1873 * u - 0.4681 * v,
                    y + 1.8556 * u);
                return fixed4(saturate(rgb), 1.0);
            }
            ENDCG
        }
    }
}
