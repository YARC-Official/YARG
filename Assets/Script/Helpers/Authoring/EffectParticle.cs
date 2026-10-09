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

        // Scales this particle's start color RGB (not alpha) by the given factor, regardless of
        // which mode (single color, single gradient, random between two colors/gradients) the
        // field is actually using. This is applied after any InitializeColor/SetColor tinting,
        // so it boosts the particle's actual (already fret-colored) RGB into HDR range - which,
        // combined with this particle's additive blend mode and the scene's bloom, reads as a
        // genuinely brighter/more vivid flash rather than one simply capped at non-HDR white.
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