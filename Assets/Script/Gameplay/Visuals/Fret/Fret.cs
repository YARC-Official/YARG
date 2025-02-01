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
        private bool  _colorChangeEnabled = false;
        private bool  _fadeDirection    = true;
        // True is pulsing, false is fading
        private bool  _pulseOrFade       = true;
        private float _fadeDuration;
        private float _fadeStartTime;
        private float _fadeEndTime;
        private float _fadeAmount = 0.0f;

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

        // TODO: May need to take a duration here..
        public void DimColor()
        {
            _active = false;
            _fadeDirection = false;
            _colorChangeEnabled = true;
            _fadeAmount = 0.0f;

            FadeColor(0.25f, false, false);
            // foreach (var material in _topMaterials)
            // {
            //     material.color = _inactiveColor;
            // }
            //
            // foreach (var material in _innerMaterials)
            // {
            //     material.color = _inactiveColor;
            // }
        }

        public void ResetColor()
        {
            _active = true;
            _fadeDirection = true;
            _colorChangeEnabled = false;
            _fadeAmount = 0.0f;
            foreach (var material in _topMaterials)
            {
                material.color = _originalUnityTopColor;
            }

            foreach (var material in _innerMaterials)
            {
                material.color = _originalUnityInnerColor;
            }
        }

        /// <summary>
        /// Fades or pulses the fret color between the normal color and inactive color
        ///
        /// Note that the pulse happens only once per call, it does not continue indefinitely.
        /// </summary>
        /// <param name="duration">Length of time transition will take</param>
        /// <param name="pulse">Whether to pulse or to fade once</param>
        /// <param name="fadeDirection">True fades in, false fades out (default true)</param>
        public void FadeColor(float duration, bool pulse, bool fadeDirection = true)
        {
            if (_active && pulse)
            {
                // Can't pulse if the fret is already active
                return;
            }

            var rateAdjustment = pulse ? 2 : 1;

            _pulseOrFade = pulse;
            _fadeDuration = duration;
            _fadeDirection = fadeDirection;
            _fadeStartTime = Time.time;
            _fadeEndTime = Time.time + (_fadeDuration / rateAdjustment);
            _colorChangeEnabled = true;
            _fadeAmount = 0.0f;
        }

        public void UpdateColor()
        {
            if (!_colorChangeEnabled)
            {
                return;
            }

            var rateAdjustment = _pulseOrFade ? 2 : 1;

            if (!_pulseOrFade && Time.time - _fadeStartTime >= _fadeDuration / rateAdjustment)
            {
                // Switch directions (
                _fadeDirection = !_fadeDirection;
                _fadeAmount = 0.0f;
            }

            _fadeAmount += Time.deltaTime / (_fadeDuration / rateAdjustment);

            if (_fadeDirection)
            {
                // Fading in
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, _fadeAmount);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, _fadeAmount);
                }
            }
            else
            {
                // Fading out
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, _fadeAmount);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, _fadeAmount);
                }
            }

            if (Time.time - _fadeStartTime >= _fadeDuration)
            {
                _colorChangeEnabled = false;
                _fadeAmount = 0.0f;

                if (_fadeAmount >= 1.0f)
                {
                    _colorChangeEnabled = false;
                    _fadeAmount = 0.0f;
                }

                // This seems like it shouldn't be necessary, but even when _fadeAmount is 1.0f we still aren't quite
                // all the way to the inactive color.
                // if (_fadeDirection)
                // {
                //     return;
                // }
                //
                // foreach (var material in _topMaterials)
                // {
                //     material.color = _inactiveColor;
                // }
                //
                // foreach (var material in _innerMaterials)
                // {
                //     material.color = _inactiveColor;
                // }
            }
        }
    }
}
