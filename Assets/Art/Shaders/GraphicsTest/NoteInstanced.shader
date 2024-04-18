Shader "YARG/NoteInstanced"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

	SubShader
    {
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint instanceID_2 : SV_InstanceID;
            };

            sampler2D _Texture;

            UNITY_INSTANCING_BUFFER_START(Properties)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Properties)

            VertexOutput vert(VertexInput input, uint instanceID_2 : SV_InstanceID)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                VertexOutput output;

                // output.vertex = UnityObjectToClipPos(input.vertex);
                output.vertex = mul(UNITY_MATRIX_MVP, input.vertex);
                output.uv = input.uv;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.instanceID_2 = instanceID_2;

                return output;
            }

            float4 frag(VertexOutput input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 color = UNITY_ACCESS_INSTANCED_PROP(Properties, _Color);
                float4 albedo = tex2D(_Texture, input.uv);
                return color * albedo;
            }

            ENDHLSL
        }
	}
}
