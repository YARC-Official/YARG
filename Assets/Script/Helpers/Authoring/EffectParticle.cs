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

        // Scales this particle's start lifetime by the given factor, regardless of whether
        // it's authored as a constant, a random range, or a curve. Used to make specific named
        // particles (see Fret.ApplyCircularDrumHitEffectTuning) hold visibly longer, without
        // touching any particle system that isn't explicitly matched by name.
        public void ScaleStartLifetime(float factor)
        {
            var main = _particleSystem.main;
            var lifetime = main.startLifetime;
            switch (lifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    lifetime.constant *= factor;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    lifetime.constantMin *= factor;
                    lifetime.constantMax *= factor;
                    break;
                case ParticleSystemCurveMode.Curve:
                    lifetime.curveMultiplier *= factor;
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    lifetime.curveMultiplier *= factor;
                    break;
            }
            main.startLifetime = lifetime;
        }

        // Scales this particle's authored start color brightness by the given factor, pushing
        // it into HDR range so the particle reads as more vivid rather than clamping at opaque
        // white. See ScaleStartLifetime above for usage context.
        public void ScaleStartColorBrightness(float factor)
        {
            var main = _particleSystem.main;
            var startColor = main.startColor;
            switch (startColor.mode)
            {
                case ParticleSystemGradientMode.Color:
                    startColor.color = ScaleColorBrightness(startColor.color, factor);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    startColor.colorMin = ScaleColorBrightness(startColor.colorMin, factor);
                    startColor.colorMax = ScaleColorBrightness(startColor.colorMax, factor);
                    break;
            }
            main.startColor = startColor;
        }

        private static Color ScaleColorBrightness(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        // Scales this particle's vertical start size by the given factor, regardless of
        // whether it's authored as a uniform size or separate per-axis sizes. Used to shrink
        // the reused guitar sustain flame's height for Circular's kick burst (see
        // Fret.ApplyCircularDrumHitEffectTuning) without affecting its width/depth.
        public void ScaleStartSizeY(float factor)
        {
            var main = _particleSystem.main;
            if (!main.startSize3D)
            {
                main.startSize3D = true;
                main.startSizeY = main.startSizeX;
                main.startSizeZ = main.startSizeX;
            }

            var sizeY = main.startSizeY;
            switch (sizeY.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    sizeY.constant *= factor;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    sizeY.constantMin *= factor;
                    sizeY.constantMax *= factor;
                    break;
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    sizeY.curveMultiplier *= factor;
                    break;
            }
            main.startSizeY = sizeY;
        }

        // Sets this particle's horizontal (X/Z) start size to an absolute world-space size,
        // rather than a relative factor on its unknown authored baseline - used to make
        // Circular's kick burst match the button's own measured width exactly. This is only
        // exact because this prefab's particle systems use ParticleSystemScalingMode.Local
        // (confirmed in the authored theme), under which Unity scales particle size by this
        // GameObject's OWN local scale only - ignoring every ancestor's scale - so
        // worldSize / transform.localScale here gives the precise local Start Size needed,
        // without having to reason about the confusing, deeply-nested parent scale/rotation
        // chain this effect happens to live inside (authored for an unrelated guitar sustain
        // flame, not a drum kick burst).
        public void SetStartSizeXZ(float worldSize)
        {
            var main = _particleSystem.main;
            if (!main.startSize3D)
            {
                main.startSize3D = true;
                main.startSizeY = main.startSizeX;
                main.startSizeZ = main.startSizeX;
            }

            var localScale = transform.localScale;
            var sizeX = Mathf.Abs(localScale.x) > 0.0001f ? worldSize / localScale.x : worldSize;
            var sizeZ = Mathf.Abs(localScale.z) > 0.0001f ? worldSize / localScale.z : worldSize;

            main.startSizeX = new ParticleSystem.MinMaxCurve(sizeX);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(sizeZ);
        }

        // Overrides this particle system's world-space Y position directly (leaving X/Z alone),
        // bypassing its own authored local position entirely. Used to pull Circular's kick burst
        // down to sit right at the button's own height - the reused guitar sustain flame's
        // emitter lives inside a parent object rotated ~180 degrees about a diagonal axis, so
        // adjusting its LOCAL position (as previously attempted) moves it along an axis that,
        // after that rotation, no longer points along world-up/down at all. Setting world
        // position directly sidesteps that confusion completely, regardless of any rotation
        // further up the hierarchy.
        public void PositionAtWorldHeight(float worldY)
        {
            var position = transform.position;
            position.y = worldY;
            transform.position = position;
        }

        // Zeroes out this particle's authored Start Delay so Play() reads as instant instead of
        // ramping in after a beat. Used by Circular's drum-hit bursts (see
        // Fret.PlayTimedEffectBurst), which reuse other effect groups for a single short flash
        // where any authored delay is very noticeable.
        public void ZeroStartDelay()
        {
            var main = _particleSystem.main;
            main.startDelay = 0f;
        }

        // Advances this particle system's simulation by a small amount right after Play(),
        // without restarting it, so the player never sees whatever is authored for the first
        // instant (e.g. a size/color ramp-in) - see Fret.PlayTimedEffectBurst for why this is
        // needed in addition to ZeroStartDelay.
        public void FastForward(float seconds)
        {
            _particleSystem.Simulate(seconds, withChildren: true, restart: false, fixedTimeStep: true);
            // Simulate() always pauses the system once it's done advancing - without calling
            // Play() again here, it would freeze on that fast-forwarded frame forever instead
            // of continuing to animate/decay and later actually stopping, which looked like a
            // "stuck on" sustain effect.
            _particleSystem.Play();
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

        // Stops emission AND immediately clears every already-alive particle, instead of
        // Stop()'s default of letting existing particles decay naturally over their own
        // lifetime. Used to end Circular's kick burst (see Fret.TimedEffectBurstRoutine)
        // precisely at its intended duration, with no lingering tail.
        public void StopImmediate()
        {
            _particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
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