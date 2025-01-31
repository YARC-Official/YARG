using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class Fret : MonoBehaviour, IThemeBindable<ThemeFret>
    {
        private static readonly int _fade = Shader.PropertyToID("Fade");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private static readonly int _hit = Animator.StringToHash("Hit");
        private static readonly int _pressed = Animator.StringToHash("Pressed");
        private static readonly int _sustain = Animator.StringToHash("Sustain");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [field: SerializeField]
        [field: HideInInspector]
        public ThemeFret ThemeBind { get; set; }

        private readonly List<Material> _topMaterials   = new();
        private readonly List<Material> _innerMaterials = new();

        private bool _hasPressedParam;
        private bool _hasSustainParam;

        // These need to be saved since the colors can now change during play
        // They are saved as Unity colors to avoid having to repeatedly convert
        // when transitioning between active and inactive states
        private UnityEngine.Color _originalUnityTopColor;
        private UnityEngine.Color _originalUnityInnerColor;

        // TODO: Consider making this customizable or perhaps just a desaturated and dimmed version of the base color
        private UnityEngine.Color _inactiveColor = new(0.321f, 0.321f, 0.321f, 1.0f);

        private bool  _active            = true;
        private bool  _colorPulseEnabled = false;
        private bool  _pulseDirection    = true;
        private float _pulseDuration;
        private float _pulseStartTime;
        private float _pulseEndTime;
        private float _pulseAmount = 0.0f;

        public void Initialize(Color top, Color inner, Color particles)
        {
            _originalUnityTopColor = top.ToUnityColor();
            _originalUnityInnerColor = inner.ToUnityColor();

            // Set the top material color
            foreach (var material in ThemeBind.GetColoredMaterials())
            {
                material.color = top.ToUnityColor();
                material.SetColor(_emissionColor, top.ToUnityColor() * 11.5f);
                _topMaterials.Add(material);
            }

            // Set the inner material color
            foreach (var material in ThemeBind.GetInnerColoredMaterials())
            {
                material.color = inner.ToUnityColor();
                _innerMaterials.Add(material);
            }

            // Set the particle colors
            ThemeBind.HitEffect.SetColor(particles.ToUnityColor());
            ThemeBind.SustainEffect.SetColor(particles.ToUnityColor());
            ThemeBind.PressedEffect.SetColor(particles.ToUnityColor());

            // See if certain parameters exist
            _hasPressedParam = ThemeBind.Animator.HasParameter(_pressed);
            _hasSustainParam = ThemeBind.Animator.HasParameter(_sustain);
        }

        // TODO: Maybe we don't want an Update function on the fret itself?
        public void Update()
        {
            UpdateColor();
        }

        public void SetPressed(bool pressed)
        {
            float value = pressed ? 1f : 0f;
            foreach (var material in _innerMaterials)
            {
                material.SetFloat(_fade, value);
            }

            if (_hasPressedParam)
            {
                ThemeBind.Animator.SetBool(_pressed, pressed);
            }

            if (pressed)
            {
                ThemeBind.PressedEffect.Play();
            }
            else
            {
                ThemeBind.PressedEffect.Stop();
            }
        }

        public void PlayHitAnimation()
        {
            ThemeBind.Animator.SetTrigger(_hit);
        }

        public void PlayHitParticles()
        {
            ThemeBind.HitEffect.Play();
        }

        public void SetSustained(bool sustained)
        {
            if (sustained)
            {
                ThemeBind.SustainEffect.Play();
            }
            else
            {
                ThemeBind.SustainEffect.Stop();
            }

            if (_hasSustainParam)
            {
                ThemeBind.Animator.SetBool(_sustain, sustained);
            }
        }

        // TODO: Lerp the color changes over time
        public void DimColor()
        {
            _active = false;
            _pulseDirection = true;
            _colorPulseEnabled = false;
            _pulseAmount = 0.0f;
            foreach (var material in _topMaterials)
            {
                material.color = _inactiveColor;
            }

            foreach (var material in _innerMaterials)
            {
                material.color = _inactiveColor;
            }
        }

        public void ResetColor()
        {
            _active = true;
            _pulseDirection = true;
            _colorPulseEnabled = false;
            _pulseAmount = 0.0f;
            foreach (var material in _topMaterials)
            {
                material.color = _originalUnityTopColor;
            }

            foreach (var material in _innerMaterials)
            {
                material.color = _originalUnityInnerColor;
            }
        }

        public void PulseColor(float duration)
        {
            if (_active)
            {
                // Can't pulse if the fret is already active
                return;
            }
            _pulseDuration = duration;
            _pulseStartTime = Time.time;
            _pulseEndTime = Time.time + (_pulseDuration / 2);
            _colorPulseEnabled = true;
            _pulseAmount = 0.0f;
        }

        public void UpdateColor()
        {
            if (!_colorPulseEnabled)
            {
                return;
            }

            if (Time.time - _pulseStartTime >= _pulseDuration / 2)
            {
                _pulseDirection = false;
                _pulseAmount = 0.0f;
            }

            _pulseAmount += Time.deltaTime / (_pulseDuration / 2);

            if (_pulseDirection)
            {
                // Fading in
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, _pulseAmount);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, _pulseAmount);
                }
            }
            else
            {
                // Fading out
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, _pulseAmount);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, _pulseAmount);
                }
            }
            if (Time.time - _pulseStartTime >= _pulseDuration)
            {
                _colorPulseEnabled = false;
                _pulseAmount = 0.0f;
            }
        }
    }
}
