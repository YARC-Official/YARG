// Floating sparkles: a field of drifting, twinkling sparkles mapped in WORLD
// space on the dominant plane of the surface (XY for vertical quads and UI,
// XZ for floors). _Scale/_Density are in the renderer's own units: world units
// for meshes, canvas pixels for uGUI elements (a 50x430-px meter wants
// _Scale ~ 18, a 2m venue quad ~ 0.35).
//
// Placement: a staggered jittered lattice. Each cell rolls a random position
// across its full area, odd rows are shifted half a cell, and every fragment
// accumulates the 3x3 neighborhood, so sparkles drift freely across cell
// borders - no visible columns, no sparkles cut in half, no window edges.
//
// Uses the game state texture (Assets/Art/Shaders/gamestate.hlsl):
//   _STAR_POWER_ONLY - nothing renders unless star power is active
//   _BEAT_PULSE      - sparkles brighten sharply on every beat
//
// UI Mask support: the stencil block's values are driven by uGUI's
// StencilMaterial clone when rendered under a Mask (no properties exposed).

Shader "YARG/Gameplay/FloatingSparkles"
{
    Properties
    {
        _SparkleTex ("Sparkle Texture", 2D) = "white" {}
        _Color      ("Sparkle Color", Color) = (1, 1, 1, 1)
        _Scale      ("Sparkle Scale", Range(0.05, 50.0)) = 0.35
        _Density    ("Sparkle Density", Range(0.25, 20.0)) = 1.75
        _Fill       ("Cell Fill", Range(0.0, 1.0)) = 0.6
        _Speed      ("Speed", Float) = 0.5

        [NoScaleOffset] _NoiseTex ("Noise Texture", 2D) = "grey" {}

        [Toggle(_GLOW)] _Glow ("Glow", Float) = 1
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _GlowAmount ("Glow Amount", Range(0.0, 800.0)) = 2.0

        [Toggle(_STAR_POWER_ONLY)] _StarPowerOnly ("Only Show When Star Power Active", Float) = 0
        [Toggle(_BEAT_PULSE)] _BeatPulse ("Pulse With The Beat", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            // Driven by uGUI's StencilMaterial clone when under a UI Mask;
            // defaults (Ref 0, Always, Keep) are a no-op everywhere else.
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _GLOW
            #pragma shader_feature_local _STAR_POWER_ONLY
            #pragma shader_feature_local _BEAT_PULSE

            #include "UnityCG.cginc"
            #include "Assets/Art/Shaders/gamestate.hlsl"

            // The sparkle sprite fills this fraction of the cell width.
            #define CORE_FRACTION 0.85
            // Sample the sprite texture within this inset of its [0,1] range:
            // keeps mip/bilinear filtering away from the border texels, which
            // would otherwise leak faint lines around every sparkle.
            #define TEX_MARGIN 0.12

            sampler2D _SparkleTex;
            sampler2D _NoiseTex;
            float4 _Color;
            float _Scale;
            float _Density;
            float _Fill;
            float _Speed;
            float4 _GlowColor;
            float _GlowAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // World-space mapping keeps the cells square on any mesh: a
                // stretched quad stretches the world positions, not the cells
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // Per-cell random values from the noise texture. Noise textures
            // commonly have clamp wrap, so fold coordinates into [0, 1) first.
            // tex2Dlod: cell coords are discontinuous, so implicit derivatives
            // would push border pixels onto garbage mip levels.
            float4 CellRandom(float2 cell)
            {
                float2 uv = frac(cell * float2(0.1031, 0.6180)) * 0.96 + 0.02;
                return tex2Dlod(_NoiseTex, float4(uv, 0, 0));
            }

            // Accumulate one sparkle's contribution into acc
            void AccumulateSparkle(float2 field, float2 cell, float brightness,
                                   inout fixed3 acc)
            {
                float4 rnd = CellRandom(cell);

                // Dropout: only some cells hold a sparkle
                float drop = step(1.0 - _Fill, frac(rnd.a * 7.13 + rnd.b));
                UNITY_BRANCH
                if (drop < 0.5) return;

                // Stagger odd rows half a cell, then jitter across the cell
                float stagger = 0.5 * frac(floor(cell.y) * 0.5);
                float2 center = cell + 0.5 + float2(stagger, 0)
                              + (rnd.rg - 0.5) * 0.9;

                // Sample the sprite centered on the sparkle position
                float2 delta = field - center;
                float2 suv = delta / (CORE_FRACTION * 0.5) + 0.5;
                float2 rsuv = suv * (1.0 - TEX_MARGIN * 2.0) + TEX_MARGIN;
                float shape = tex2Dlod(_SparkleTex, float4(rsuv, 0, 0)).a;
                // Zero the sprite's border texels in-shader: the source PNG
                // has faint nonzero alpha at its edges that bleeds as lines
                shape *= smoothstep(0.0, 0.06, suv.x) * smoothstep(0.0, 0.06, 1.0 - suv.x)
                       * smoothstep(0.0, 0.06, suv.y) * smoothstep(0.0, 0.06, 1.0 - suv.y);

                // Pow sharpens the star core against the sprite's soft edges
                float coreA = pow(shape, 1.3);

                // Twinkle: pulsing brightness, unique phase per sparkle
                float twinkle = 0.35 + 0.65 * (0.5 + 0.5 * sin(_Time.y * (2.0 + 4.0 * rnd.b) * (0.5 + _Speed) + rnd.b * 6.2831853));

                acc += _Color.rgb * twinkle * coreA;

            #ifdef _GLOW
                // Emission: HDR color/amount multiplied into the star core
                // (quadratic so it concentrates at the bright center).
                // Bloom turns this into the visible halo.
                acc += _GlowColor.rgb * _GlowAmount * twinkle * coreA * coreA;
            #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
            #ifdef _STAR_POWER_ONLY
                clip(YargGameStateStarPowerActive() - 0.5);
            #endif

                float cells = _Density / max(_Scale, 0.05);

                // Pick the two world axes perpendicular to the dominant
                // normal axis: vertical quads map to XY, floor planes to XZ.
                float3 an = abs(i.normal);
                float2 base = (an.z >= an.x && an.z >= an.y) ? i.world.xy
                            : (an.y >= an.x)                  ? i.world.xz
                                                              : i.world.zy;

                // Wobble the whole field with the noise texture. Sampled at a
                // fixed high LOD: raw noise (e.g. white-noise textures) would
                // otherwise jitter the field per pixel and shred the sparkles.
                float3 n = tex2Dlod(_NoiseTex, float4(frac(i.world.xy * 0.5 + float2(_Time.y * 0.03, _Time.y * 0.017)), 0, 4)).rgb;
                float2 field = base * cells + (n.xy - 0.5) * 0.8;

                // Drift upward over time
                field.y -= _Time.y * _Speed;

            #ifdef _BEAT_PULSE
                // Sharp pop on each beat, decaying smoothly
                float pulse = 1.0 + 1.5 * pow(saturate(1.0 - YargGameStateBeatPhase()), 4.0);
            #else
                float pulse = 1.0;
            #endif

                // Sum the 3x3 neighborhood: sparkles can drift across cell
                // borders without being cut by a per-cell window
                float2 cell = floor(field);
                fixed3 acc = 0;
                UNITY_UNROLL
                for (int dy = -1; dy <= 1; dy++)
                {
                    UNITY_UNROLL
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        AccumulateSparkle(field, cell + float2(dx, dy), pulse, acc);
                    }
                }

                acc *= pulse;

                // UI tint (vertex color from the CanvasRenderer)
                acc *= i.color.rgb * i.color.a;

                // Fade sparkles near the element's own border so their outer
                // halves never get hard-clipped by the mesh/UI edge. Only
                // applies when the geometry has real UVs (quads, UI rects);
                // the margin is in UV space so it hugs the element bounds.
                float2 edge = min(i.uv, 1.0 - i.uv);              // 0 at edge, 0.5 in center
                float edgeFade = saturate(min(edge.x, edge.y) / 0.08);
                acc *= edgeFade * edgeFade;

                // Premultiplied additive: framebuffer alpha untouched
                return fixed4(acc, 0.0);
            }

            ENDCG
        }
    }
}
