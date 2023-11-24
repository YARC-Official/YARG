﻿using System.Collections.Generic;
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

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.

        [SerializeField]
        private ThemeFret _themeFret;

        private readonly List<Material> _innerMaterials = new();

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
        }

        public void SetPressed(bool pressed)
        {
            float value = pressed ? 1f : 0f;
            _innerMaterials.ForEach(i => i.SetFloat(_fade, value));
        }

        public void PlayHitAnimation()
        {
            StopAnimation();
            _themeFret.Animation.Play("FretsGuitar");
        }

        public void PlayDrumAnimation()
        {
            StopAnimation();
            _themeFret.Animation.Play("FretsDrums");
        }

        public void PlayHitParticles()
        {
            _themeFret.HitEffect.Play();
        }

        public void StopAnimation()
        {
            _themeFret.Animation.Stop();
            _themeFret.Animation.Rewind();
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