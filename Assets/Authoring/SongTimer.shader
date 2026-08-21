// Digit SDFs based on "digits" by drschizzo (CC BY-NC-SA 3.0)
// https://www.shadertoy.com/view/4dc3zr
//
// Displays the song time tape-deck style, in one line:
//   MM:SS / MM:SS   (current / total)
// using the global _Yarg_GameStateTex written by TextureManager.
//
// Apply to any quad. The quad's UV space is used; scale the quad/transform
// to size the text. The layout is fixed-width, so nothing shifts as time
// advances.

Shader "YARG/SongTimer"
{
    Properties
    {
        _GlyphColor ("Current Time Color", Color) = (1.0, 0.35, 0.1, 1.0)
        _TotalColor ("Total Time Color", Color) = (0.75, 0.75, 0.75, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Game state global texture accessors (append-only layout,
            // see file header)
            #include "Assets/Art/Shaders/gamestate.hlsl"

            fixed4 _GlyphColor;
            fixed4 _TotalColor;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.pos = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                return OUT;
            }

            static const float pi  = 3.14159265;
            static const float tau = 6.28318531;

            // Layout units: one digit is drawn in a 1.0 x 1.5 box.
            // The whole "MM:SS / MM:SS" string is 12.0 units wide, so this
            // scale fits it inside a quad with a bit of margin.
            static const float GLYPH_SCALE = 1.0 / 14.0;

            // Horizontal advance per character, in layout units
            static const float ADV_DIGIT = 1.1;
            static const float ADV_COLON = 0.6;
            static const float ADV_SLASH = 0.8;
            static const float ADV_SPACE = 0.6;

            // Nixie tube look
            // Filament half-thickness, in layout units (digit height = 1.5)
            static const float STROKE        = 0.05;
            static const float HALO          = 0.55;
            static const float HALO_FALLOFF  = 7.0;
            // How much the hot filament core shifts toward white
            static const float CORE_HOTNESS  = 0.65;

            // GLSL mod (result takes sign of divisor), unlike HLSL fmod
            #define glslMod(x, y) ((x) - (y) * floor((x) / (y)))

            // Distance to a line segment,
            float dfLine(float2 start, float2 end, float2 uv)
            {
                float2 seg = end - start;
                float t = dot(uv - start, seg) / dot(seg, seg);
                return distance(start + seg * clamp(t, 0.0, 1.0), uv);
            }

            // Distance to the edge of a circle.
            float dfCircle(float2 origin, float radius, float2 uv)
            {
                return abs(length(uv - origin) - radius);
            }

            // Distance to an arc.
            float dfArc(float2 origin, float start, float sweep, float radius, float2 uv)
            {
                uv -= origin;
                // NOTE: HLSL float2x2 is row-major (GLSL mat2 is column-major),
                // so the arguments are swapped vs the original to rotate by -start
                uv = mul(uv, float2x2(cos(start), -sin(start), sin(start), cos(start)));

                float offs = (sweep / 2.0 - pi);
                float ang = glslMod(atan2(uv.y, uv.x) - offs, tau) + offs;
                ang = clamp(ang, min(0.0, sweep), max(0.0, sweep));

                return distance(radius * float2(cos(ang), sin(ang)), uv);
            }

            // Distance to the digit "d" (0-9).
            // Digit occupies a 1.0 x 1.5 box with its lower-left corner at "origin".
            float dfDigit(float2 origin, float d, float2 uv)
            {
                uv -= origin;
                d = floor(d);
                float dist = 1e6;

                // Single exit: conditions are mutually exclusive (floor(d)),
                // so accumulating through every branch keeps DXIL validation happy.
                if (d == 0.0)
                {
                    dist = min(dist, dfLine(float2(1.000, 1.000), float2(1.000, 0.500), uv));
                    dist = min(dist, dfLine(float2(0.000, 1.000), float2(0.000, 0.500), uv));
                    dist = min(dist, dfArc(float2(0.500, 1.000), 0.000, 3.142, 0.500, uv));
                    dist = min(dist, dfArc(float2(0.500, 0.500), 3.142, 3.142, 0.500, uv));
                }
                if (d == 1.0)
                {
                    dist = min(dist, dfLine(float2(0.500, 1.500), float2(0.500, 0.000), uv));
                }
                if (d == 2.0)
                {
                    dist = min(dist, dfLine(float2(1.000, 0.000), float2(0.000, 0.000), uv));
                    dist = min(dist, dfLine(float2(0.388, 0.561), float2(0.806, 0.719), uv));
                    dist = min(dist, dfArc(float2(0.500, 1.000), 0.000, 3.142, 0.500, uv));
                    dist = min(dist, dfArc(float2(0.700, 1.000), 5.074, 1.209, 0.300, uv));
                    dist = min(dist, dfArc(float2(0.600, 0.000), 1.932, 1.209, 0.600, uv));
                }
                if (d == 3.0)
                {
                    dist = min(dist, dfLine(float2(0.000, 1.500), float2(1.000, 1.500), uv));
                    dist = min(dist, dfLine(float2(1.000, 1.500), float2(0.500, 1.000), uv));
                    dist = min(dist, dfArc(float2(0.500, 0.500), 3.142, 4.712, 0.500, uv));
                }
                if (d == 4.0)
                {
                    dist = min(dist, dfLine(float2(0.700, 1.500), float2(0.000, 0.500), uv));
                    dist = min(dist, dfLine(float2(0.000, 0.500), float2(1.000, 0.500), uv));
                    dist = min(dist, dfLine(float2(0.700, 1.200), float2(0.700, 0.000), uv));
                }
                if (d == 5.0)
                {
                    dist = min(dist, dfLine(float2(1.000, 1.500), float2(0.300, 1.500), uv));
                    dist = min(dist, dfLine(float2(0.300, 1.500), float2(0.200, 0.900), uv));
                    dist = min(dist, dfArc(float2(0.500, 0.500), 3.142, 5.356, 0.500, uv));
                }
                if (d == 6.0)
                {
                    dist = min(dist, dfLine(float2(0.067, 0.750), float2(0.500, 1.500), uv));
                    dist = min(dist, dfCircle(float2(0.500, 0.500), 0.500, uv));
                }
                if (d == 7.0)
                {
                    dist = min(dist, dfLine(float2(0.000, 1.500), float2(1.000, 1.500), uv));
                    dist = min(dist, dfLine(float2(1.000, 1.500), float2(0.500, 0.000), uv));
                }
                if (d == 8.0)
                {
                    dist = min(dist, dfCircle(float2(0.500, 0.400), 0.400, uv));
                    dist = min(dist, dfCircle(float2(0.500, 1.150), 0.350, uv));
                }
                if (d == 9.0)
                {
                    dist = min(dist, dfLine(float2(0.933, 0.750), float2(0.500, 0.000), uv));
                    dist = min(dist, dfCircle(float2(0.500, 1.000), 0.500, uv));
                }

                return dist;
            }

            // Two dots, vertically stacked. Occupies an ADV_COLON-wide cell.
            float dfColon(float2 origin, float2 uv)
            {
                float2 c = origin + float2(ADV_COLON * 0.5, 0.0);
                float dist = dfCircle(c + float2(0.0, 0.40), 0.07, uv);
                dist = min(dist, dfCircle(c + float2(0.0, 1.10), 0.07, uv));
                return dist;
            }

            // Diagonal stroke. Occupies an ADV_SLASH-wide cell.
            float dfSlash(float2 origin, float2 uv)
            {
                return dfLine(origin + float2(0.10, 0.00), origin + float2(0.70, 1.50), uv);
            }

            // "MM:SS" starting at "origin". Returns the distance.
            float dfClock(float2 origin, float time, float2 uv)
            {
                time = max(time, 0.0);
                float minutes = floor(time / 60.0);
                float seconds = floor(glslMod(time, 60.0));

                float mTens = floor(glslMod(minutes / 10.0, 10.0));
                float mOnes = glslMod(minutes, 10.0);
                float sTens = floor(glslMod(seconds / 10.0, 10.0));
                float sOnes = glslMod(seconds, 10.0);

                float dist = 1e6;

                dist = min(dist, dfDigit(origin + float2(0.0 * ADV_DIGIT, 0.0), mTens, uv));
                dist = min(dist, dfDigit(origin + float2(1.0 * ADV_DIGIT, 0.0), mOnes, uv));
                dist = min(dist, dfColon(origin + float2(2.0 * ADV_DIGIT, 0.0), uv));
                dist = min(dist, dfDigit(origin + float2(2.0 * ADV_DIGIT + ADV_COLON, 0.0), sTens, uv));
                dist = min(dist, dfDigit(origin + float2(3.0 * ADV_DIGIT + ADV_COLON, 0.0), sOnes, uv));

                return dist;
            }

            // Nixie tube shading: a thin bright filament with a soft glowing
            // halo around it, like the original shadertoy's 0.004/dist look,
            // but capped so YARG's bloom doesn't blow it out.
            // Uses fwidth-based AA so the thin strokes stay readable at any
            // quad size / viewing distance.
            float intensity(float dist)
            {
                float d = abs(dist);
                float aa = max(fwidth(dist), 1e-5);
                float core = 1.0 - smoothstep(STROKE - aa, STROKE + aa, d);
                float halo = HALO * exp(-d * HALO_FALLOFF);
                return core + halo;
            }

            float4 mainImage(float2 quadUv)
            {
                float songLength   = YargGameStateSongLength();
                float songPosition = YargGameStateSongPosition();

                // Centered coordinates in layout units
                float2 uv = (quadUv - 0.5) / GLYPH_SCALE;
                uv.y += 0.75; // vertically center the 1.5-tall glyphs

                // Total string width:
                // clock (4*ADV_DIGIT+ADV_COLON) + space + slash + space + clock
                float clockW = 4.0 * ADV_DIGIT + ADV_COLON;
                float totalW = clockW + ADV_SPACE + ADV_SLASH + ADV_SPACE + clockW;

                float2 origin = float2(-totalW / 2.0, 0.0);

                // Current time
                float distCur = dfClock(origin, songPosition, uv);

                // " / "
                float2 sepOrigin = origin + float2(clockW + ADV_SPACE, 0.0);
                float distSep = dfSlash(sepOrigin, uv);

                // Total time
                float distTot = dfClock(sepOrigin + float2(ADV_SLASH + ADV_SPACE, 0.0), songLength, uv);

                // Nixie tube shading
                float cur = intensity(distCur);
                float sep = intensity(distSep);
                float tot = intensity(distTot);

                // Hot whitish filament core over the saturated base color,
                // like a real nixie tube
                float3 color = float3(0, 0, 0);
                color += (lerp(_GlyphColor.rgb, float3(1, 1, 1), CORE_HOTNESS) * cur);
                color += (_GlyphColor.rgb * sep);
                color += (_TotalColor.rgb * tot);

                // Alpha from glyph coverage only, so the quad's background
                // stays fully transparent
                float alpha = saturate(max(max(cur, sep), tot));

                // Gamma correction for YARG
                color = pow(color, 2.2);

                return float4(color, alpha);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                return mainImage(IN.uv);
            }

            ENDCG
        }
    }
}
