using System.Collections.Generic;
using UnityEngine;

namespace YARG.Helpers.Authoring
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in theme creation.
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    [RequireComponent(typeof(ParticleSystem))]
    public class EffectParticle : MonoBehaviour
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _blendProp = Shader.PropertyToID("_Blend");
        private static readonly int _srcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int _dstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int _srcBlendAlpha = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int _dstBlendAlpha = Shader.PropertyToID("_DstBlendAlpha");

        [Space]
        [SerializeField]
        private bool _allowColoring = true;
        [SerializeField]
        private bool _keepAlphaWhenColoring = true;

        [Space]
        [SerializeField]
        private bool _setEmissionWhenColoring;
        [SerializeField]
        private float _emissionColorMultiplier = 1f;

        [Space]
        [SerializeField]
        private bool _modifyInBre;

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleSystemRenderer;

        private ParticleSettings _normalSettings;
        private ParticleSettings _breSettings = new ParticleSettings
        {
            StartSpeedMultiplier = 2f,
            SparkleStartLifetimeMultiplier = 1.2f,
            MaxParticles = 10000,
            MinCountMultiplier = 5,
            MaxCountMultiplier = 5,
        };

        private bool _breMode = false;

        private Color InitialColor { get; set; }
        private Color BrightColor  => Color.Lerp(InitialColor, Color.white, 0.75f);

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _particleSystemRenderer = GetComponent<ParticleSystemRenderer>();

            var main = _particleSystem.main;
            var emitter = _particleSystem.emission;
            var shape = _particleSystem.shape;
            var rotation = _particleSystem.transform.rotation;

            var burstList = new List<ParticleSystem.Burst>();
            if (emitter.burstCount > 0)
            {
                for (int i = 0; i < emitter.burstCount; i++)
                {
                    burstList.Add(emitter.GetBurst(i));
                }
            }

            _normalSettings = new ParticleSettings
            {
                Rotation = rotation,
                StartSpeedMultiplier = main.startSpeedMultiplier,
                SparkleStartLifetimeMultiplier = main.startLifetimeMultiplier,
                MaxParticles = main.maxParticles,
                Burst = burstList,
                ShapeType = shape.shapeType,
                RandomDirectionAmount = shape.randomDirectionAmount,
                OtherStartLifetimeMultiplier = main.startLifetimeMultiplier
            };

            // Annoyingly, we have to set the bre settings rotation here since we don't know what the original is
            // in advance
            var breRotation = rotation;
            breRotation.x = 180;
            _breSettings.Rotation = breRotation;
        }

        public void InitializeColor(Color color)
        {
            SetColor(color);
            InitialColor = color;
        }

        // Re-derives InitialColor from itself, scaled by the same factor as
        // ScaleStartColorBrightness below. Needed because InitialColor is what
        // RestoreColor()/BrightenColor() reset startColor to whenever the fret's accent-whiten
        // flash (see Fret.WhitenFretColor/RestoreFretColor) plays - without this, that flash
        // would silently discard a brightness boost the first time it fires.
        public void RebaseInitialColorBrightness(float factor)
        {
            InitialColor = ScaleColorBrightness(InitialColor, factor);
        }

        public void BrightenColor()
        {
            SetColor(BrightColor);
        }

        public void RestoreColor()
        {
            SetColor(InitialColor);
        }

        private void SetColor(Color color)
        {
            if (!_allowColoring) return;

            // Get the main particle module
            var m = _particleSystem.main;

            // Get the preferred color
            var c = color;
            if (_keepAlphaWhenColoring)
            {
                c.a = m.startColor.color.a;
            }

            // Set the color
            m.startColor = c;

            // Now try to set the emission color
            if (!_setEmissionWhenColoring || _particleSystemRenderer == null) return;

            // Set the emission color
            var material = _particleSystemRenderer.material;
            material.color = color;
            material.SetColor(_emissionColor, color * _emissionColorMultiplier);
        }

        public void Play()
        {
            // Prevent double starts
            if (_particleSystem.main.loop && _particleSystem.isEmitting) return;

            _particleSystem.Play();
        }

        public void Stop()
        {
            // Prevent double stops
            if (_particleSystem.main.loop && !_particleSystem.isEmitting) return;

            _particleSystem.Stop();
        }

        // Scales this particle's start lifetime by the given factor, regardless of
        // whether it's authored as a constant, a random range, or a curve.
        public void ScaleStartLifetime(float factor)
        {
            var main = _particleSystem.main;
            main.startLifetime = ScaleCurve(main.startLifetime, factor);
        }

        // Scales this particle's velocity-over-lifetime (x/y/z) by the given factor.
        // No-ops if the module isn't enabled on this particle system.
        public void ScaleVelocityOverLifetime(float factor)
        {
            var velocity = _particleSystem.velocityOverLifetime;
            if (!velocity.enabled) return;

            velocity.x = ScaleCurve(velocity.x, factor);
            velocity.y = ScaleCurve(velocity.y, factor);
            velocity.z = ScaleCurve(velocity.z, factor);
        }

        // Scales this particle's Y-axis start size only (not X/Z) - used to stretch a thin
        // particle taller without also making it wider.
        public void ScaleStartSizeY(float factor)
        {
            var main = _particleSystem.main;
            main.startSizeY = ScaleCurve(main.startSizeY, factor);
        }

        // Switches this particle's material from the default alpha blend to additive. Uses a
        // per-instance material copy (Unity auto-instances on first access to .material), so
        // this never affects the shared asset or other instances using the same prefab.
        public void MakeAdditive()
        {
            if (_particleSystemRenderer == null) return;

            var material = _particleSystemRenderer.material;
            material.SetFloat(_blendProp, 2f); // Additive
            material.SetFloat(_srcBlend, (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(_dstBlend, (float) UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(_srcBlendAlpha, (float) UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(_dstBlendAlpha, (float) UnityEngine.Rendering.BlendMode.One);
        }

        // Scales this particle's start color RGB (not alpha) by the given factor, so it reads
        // as a brighter/more vivid flash (combined with additive blending + scene bloom) rather
        // than one capped at non-HDR white.
        public void ScaleStartColorBrightness(float factor)
        {
            var main = _particleSystem.main;
            main.startColor = ScaleGradientBrightness(main.startColor, factor);
        }

        private static ParticleSystem.MinMaxGradient ScaleGradientBrightness(ParticleSystem.MinMaxGradient gradient, float factor)
        {
            gradient.color = ScaleColorBrightness(gradient.color, factor);
            gradient.colorMin = ScaleColorBrightness(gradient.colorMin, factor);
            gradient.colorMax = ScaleColorBrightness(gradient.colorMax, factor);

            // The gradient fields are reference types and may be unset for modes that don't use
            // them (e.g. single/two-color mode), so only scale the ones actually present.
            if (gradient.gradient != null) gradient.gradient = ScaleGradientColorKeys(gradient.gradient, factor);
            if (gradient.gradientMin != null) gradient.gradientMin = ScaleGradientColorKeys(gradient.gradientMin, factor);
            if (gradient.gradientMax != null) gradient.gradientMax = ScaleGradientColorKeys(gradient.gradientMax, factor);

            return gradient;
        }

        private static Color ScaleColorBrightness(Color color, float factor)
        {
            color.r *= factor;
            color.g *= factor;
            color.b *= factor;
            return color;
        }

        private static Gradient ScaleGradientColorKeys(Gradient gradient, float factor)
        {
            var colorKeys = gradient.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = ScaleColorBrightness(colorKeys[i].color, factor);
            }
            gradient.colorKeys = colorKeys;
            return gradient;
        }

        // Raises this particle's Color Over Lifetime alpha (not its start color's alpha) toward
        // fully opaque by the given amount (0 = no change, 1 = fully opaque throughout). No-op
        // if the module isn't enabled.
        public void BoostColorOverLifetimeAlpha(float boostAmount)
        {
            var colorOverLifetime = _particleSystem.colorOverLifetime;
            if (!colorOverLifetime.enabled) return;

            colorOverLifetime.color = BoostGradientAlpha(colorOverLifetime.color, boostAmount);
        }

        private static ParticleSystem.MinMaxGradient BoostGradientAlpha(ParticleSystem.MinMaxGradient gradient, float boostAmount)
        {
            gradient.color = BoostColorAlpha(gradient.color, boostAmount);
            gradient.colorMin = BoostColorAlpha(gradient.colorMin, boostAmount);
            gradient.colorMax = BoostColorAlpha(gradient.colorMax, boostAmount);

            // The gradient fields are reference types and may be unset for modes that don't use
            // them (e.g. single/two-color mode), so only boost the ones actually present.
            if (gradient.gradient != null) gradient.gradient = BoostGradientAlphaKeys(gradient.gradient, boostAmount);
            if (gradient.gradientMin != null) gradient.gradientMin = BoostGradientAlphaKeys(gradient.gradientMin, boostAmount);
            if (gradient.gradientMax != null) gradient.gradientMax = BoostGradientAlphaKeys(gradient.gradientMax, boostAmount);

            return gradient;
        }

        private static Color BoostColorAlpha(Color color, float boostAmount)
        {
            color.a = Mathf.Lerp(color.a, 1f, boostAmount);
            return color;
        }

        private static Gradient BoostGradientAlphaKeys(Gradient gradient, float boostAmount)
        {
            // Alpha over a Gradient's lifetime lives in its alpha keys, not the color keys'
            // alpha component (which Unity ignores when evaluating a Gradient).
            var alphaKeys = gradient.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha = Mathf.Lerp(alphaKeys[i].alpha, 1f, boostAmount);
            }
            gradient.alphaKeys = alphaKeys;
            return gradient;
        }

        private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float factor)
        {
            // Scale every representation so this works no matter which mode (constant,
            // random between two constants, curve, etc.) the field is actually using.
            curve.constant *= factor;
            curve.constantMin *= factor;
            curve.constantMax *= factor;
            curve.curveMultiplier *= factor;
            return curve;
        }

        public void SetBreMode(bool breMode)
        {
            if (_breMode == breMode)
            {
                return;
            }

            _breMode = breMode;

            var main = _particleSystem.main;
            var emitter = _particleSystem.emission;
            var shape = _particleSystem.shape;

            if (breMode && _modifyInBre)
            {
                if (emitter.burstCount > 0)
                {
                    _particleSystem.transform.rotation = _breSettings.Rotation;
                    main.startSpeedMultiplier = _breSettings.StartSpeedMultiplier;
                    main.startLifetimeMultiplier = _breSettings.SparkleStartLifetimeMultiplier;
                    main.maxParticles = _breSettings.MaxParticles;

                    for (int i = 0; i < emitter.burstCount; i++)
                    {
                        var burst = emitter.GetBurst(i);
                        burst.minCount *= _breSettings.MinCountMultiplier;
                        burst.maxCount *= _breSettings.MaxCountMultiplier;
                        emitter.SetBurst(i, burst);
                    }
                }
                else
                {
                    shape.shapeType = _breSettings.ShapeType;
                    shape.randomDirectionAmount = _breSettings.RandomDirectionAmount;
                    main.startLifetimeMultiplier = _breSettings.OtherStartLifetimeMultiplier;
                }
            }
            else if (_modifyInBre)
            {
                _particleSystem.transform.rotation = _normalSettings.Rotation;
                if (emitter.burstCount > 0)
                {
                    _particleSystem.transform.rotation = _normalSettings.Rotation;
                    main.startSpeedMultiplier = _normalSettings.StartSpeedMultiplier;
                    main.startLifetimeMultiplier = _normalSettings.SparkleStartLifetimeMultiplier;
                    main.maxParticles = _normalSettings.MaxParticles;

                    for (int i = 0; i < emitter.burstCount; i++)
                    {
                        emitter.SetBurst(i, _normalSettings.Burst[i]);
                    }
                }
                else
                {
                    shape.shapeType = _normalSettings.ShapeType;
                    shape.randomDirectionAmount = _normalSettings.RandomDirectionAmount;
                    main.startLifetimeMultiplier = _normalSettings.OtherStartLifetimeMultiplier;
                }
            }
        }

        private struct ParticleSettings
        {
            // Sparkle and Shard settings
            public Quaternion                 Rotation;
            public float                      StartSpeedMultiplier;
            public float                      SparkleStartLifetimeMultiplier;
            public int                        MaxParticles;
            public short                      MinCountMultiplier;
            public short                      MaxCountMultiplier;
            public List<ParticleSystem.Burst> Burst;

            // Other particle type settings
            public ParticleSystemShapeType ShapeType;
            public float RandomDirectionAmount;
            public float OtherStartLifetimeMultiplier;
        }
    }
}