Shader "Trails"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PrevFrame ("Texture", 2D) = "black" {}
        _FrameCount ("Frames to blend", Integer) = 4
        _Length ("Trail Length", Float) = 0.5
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        // Blend to get a trail effect
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _Length;
            UNITY_DECLARE_TEX2DARRAY(_PrevFrames);

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                col.a = _Length;

                return col;
            }
            ENDCG
        }
    }
}