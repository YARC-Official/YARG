using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class Fret : MonoBehaviour
    {
        private static readonly int _fade = Shader.PropertyToID("Fade");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private static readonly int _hit = Animator.StringToHash("Hit");
        private static readonly int _pressed = Animator.StringToHash("Pressed");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.

        [SerializeField]
        private ThemeFret _themeFret;

        private readonly List<Material> _innerMaterials = new();

        private bool _hasPressedParam;

        public void Initialize(Color top, Color inner, Color particles)
        {
            // Set the top material color
            foreach (var material in _themeFret.GetColoredMaterials())
            {
                material.color = top.ToUnityColor();
                material.SetColor(_emissionColor, top.ToUnityColor() * 11.5f);
            }

            // Set the inner material color
            foreach (var material in _themeFret.GetInnerColoredMaterials())
            {
                material.color = inner.ToUnityColor();
                _innerMaterials.Add(material);
            }

            // Set the particle colors
            _themeFret.HitEffect.SetColor(particles.ToUnityColor());
            _themeFret.SustainEffect.SetColor(particles.ToUnityColor());
            _themeFret.PressedEffect.SetColor(particles.ToUnityColor());

            // See if certain parameters exist
            _hasPressedParam = _themeFret.Animator.HasParameter(_pressed);
        }

        public void SetPressed(bool pressed)
        {
            float value = pressed ? 1f : 0f;
            _innerMaterials.ForEach(i => i.SetFloat(_fade, value));

            if (_hasPressedParam)
            {
                _themeFret.Animator.SetBool(_pressed, pressed);
            }

            if (pressed)
            {
                _themeFret.PressedEffect.Play();
            }
            else
            {
                _themeFret.PressedEffect.Stop();
            }
        }

        public void PlayHitAnimation()
        {
            _themeFret.Animator.SetTrigger(_hit);
        }

        public void PlayHitParticles()
        {
            _themeFret.HitEffect.Play();
        }

        public void SetSustained(bool sustained)
        {
            if (sustained)
            {
                _themeFret.SustainEffect.Play();
            }
            else
            {
                _themeFret.SustainEffect.Stop();
            }
        }

        public static void CreateFromThemeFret(ThemeFret themeFret)
        {
            var fretComp = themeFret.gameObject.AddComponent<Fret>();
            fretComp._themeFret = themeFret;
        }
    }
}