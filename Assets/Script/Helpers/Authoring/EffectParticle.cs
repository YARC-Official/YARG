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