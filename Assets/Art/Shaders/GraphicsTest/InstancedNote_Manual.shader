Shader "YARG/NoteInstanced"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

	SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "ForceNoShadowCasting" = "True"
            "PreviewType" = "Plane"
        }

        LOD 100

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma require instancing : INSTANCING_ON PROCEDURAL_INSTANCING_ON
            #pragma instancing_options procedural:InstanceTransform_setup

            #include "UnityCG.cginc"
            #include "Assets/Art/Shaders/Instancing/InstanceTransform.hlsl"
            #include "Assets/Art/Shaders/Instancing/InstanceColor.hlsl"

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
            };

            sampler2D _Texture;

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            VertexOutput vert(const VertexInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                VertexOutput output;

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                return output;
            }

            float4 frag(const VertexOutput input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color);
                GetInstanceColor_float(color, color);

                const float4 albedo = tex2D(_Texture, input.uv);
                return color * albedo;
            }

            ENDHLSL
        }
	}
}
