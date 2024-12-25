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

        private readonly List<Material> _innerMaterials = new();

        private bool _hasPressedParam;
        private bool _hasSustainParam;

        public void Initialize(Color top, Color inner, Color particles)
        {
            // Set the top material color
            foreach (var material in ThemeBind.GetColoredMaterials())
            {
                material.color = top.ToUnityColor();
                material.SetColor(_emissionColor, top.ToUnityColor() * 11.5f);
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
    }
}
