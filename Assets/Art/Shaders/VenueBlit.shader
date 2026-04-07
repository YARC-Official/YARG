Shader "YargBackgroundUnderlay" 
{ 
   SubShader 
   { 
       Tags { "RenderPipeline" = "UniversalPipeline" } 
       ZWrite Off Cull Off 
       Blend OneMinusDstAlpha DstAlpha 

       Pass 
       { 
           Name "YargBackgroundUnderlay" 
 
           HLSLPROGRAM 
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 
           #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl" 
 
           #pragma vertex Vert 
           #pragma fragment Frag 

           float _YargBackgroundAlpha;
 
           float4 Frag(Varyings input) : SV_Target0 
           { 
                // input.texcoord already has _BlitScaleBias applied from the vertex shader
                float2 uv = input.texcoord.xy; 
                
                // Use the standard macro for sampling blit textures
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel); 
                return color * _YargBackgroundAlpha; 
           } 
           ENDHLSL 
       } 
   } 
}
