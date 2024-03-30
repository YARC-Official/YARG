// https://forum.unity.com/threads/shader-how-to-write-a-shader-that-respects-sorting-order-first-and-then-depth.1170524/

Shader "Depth Buffer Clear"
{
    SubShader
    {
        Pass
        {
            // Only render to depth buffer
            ColorMask 0

            // We don't want this to be culled
            Cull Off

            ZWrite On
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Make fog work
            #pragma multi_compile_fog

            // Basically just forces the quad to be over the whole camera
            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                // Expects you're using the default Unity quad
                float4 pos = float4(vertex.xy * 2.0, 0.0, 1.0);

                // Max depth is 1.0 for OpenGL, 0.0 for everything else,
                // but if z == max depth it might get clipped, so set
                // it just slightly inside the depth range.

                #if UNITY_REVERSED_Z
                pos.z = 0.000001;
                #else
                pos.z = 0.999999;
                #endif

                return pos;
            }

            // Does not matter due to the "ColorMark 0"
            // We still need this though
            fixed4 frag() : SV_Target {
                return 0;
            }

            ENDCG
        }
    }
}