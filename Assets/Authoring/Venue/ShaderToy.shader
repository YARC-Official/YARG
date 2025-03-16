Shader "ShaderToy"
{
    Properties
    {
        [NoScaleOffset] _Yarg_SoundTex ("SoundTexture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            ColorMask RGB

            // We don't want this to be culled
            Cull Off

            ZWrite On
            ZTest Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
                
            #define iResolution _ScreenParams
            #define gl_FragCoord ((_iParam.scrPos.xy/_iParam.scrPos.w) * _ScreenParams.xy)
                
            #include "UnityCG.cginc"

            texture2D _Yarg_SoundTex;
          
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {    
                float4 pos : SV_POSITION;    
                float4 scrPos : TEXCOORD0;   
            };

            v2f vert(appdata_t v)
            {
				v2f OUT;
                // Expects you're using the default Unity quad
                // this makes it cover whole screen/camera
                float4 pos = float4(v.vertex.xy * 2.0, 0.0, 1.0);
                #if UNITY_REVERSED_Z
                pos.z = 0.000001;
                #else
                pos.z = 0.999999;
                #endif
                    
                OUT.pos = pos;
                OUT.scrPos = ComputeScreenPos(pos);
                
                return OUT;
            }
            
            float4 mainImage( float2 fragCoord )
            {
                // create pixel coordinates
            	float2 uv = fragCoord.xy / iResolution.xy;

                // the sound texture is 512x2
                int tx = int(uv.x*512.0);
    
            	// first row is frequency data (48Khz/4 in 512 texels, meaning 23 Hz per texel)
            	float fft  = _Yarg_SoundTex.Load( int3(tx,0,0)).x; 

                // second row is the sound wave, one texel is one mono sample
            	float wave  = _Yarg_SoundTex.Load( int3(tx,1,0)).x; 
	
            	// convert frequency to colors
            	float3 col = float3( fft, 4.0*fft*(1.0-fft), 1.0-fft ) * fft;

                // add wave form on top	
            	col += 1.0 -  smoothstep( 0.0, 0.15, abs(wave - uv.y) );
	
            	// output final color
            	return float4(col,1.0);
            }
                    
            fixed4 frag(v2f _iParam) : SV_Target {
                return mainImage(gl_FragCoord);
            }

            ENDCG
        }
    }
}
