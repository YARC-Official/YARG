using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Helpers.Authoring;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class Fret : MonoBehaviour
    {
        private static readonly int _fade = Shader.PropertyToID("Fade");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.

        [SerializeField]
        private EffectGroup _hitParticles;
        [SerializeField]
        private EffectGroup _sustainParticles;

        [SerializeField]
        private Animation _animation;

        [SerializeField]
        private MeshRenderer _meshRenderer;
        [SerializeField]
        private int _topIndex;
        [SerializeField]
        private int _innerIndex;

        private Material _topMaterial;
        private Material _innerMaterial;

        public void Initialize(Color top, Color inner, Color particles)
        {
            // Get materials
            var materials = _meshRenderer.materials;
            _topMaterial = materials[_topIndex];
            _innerMaterial = materials[_innerIndex];

            // Set the top material color
            _topMaterial.color = top.ToUnityColor();
            _topMaterial.SetColor("_EmissionColor", top.ToUnityColor() * 11.5f);

            // Set the inner material color
            _innerMaterial.color = inner.ToUnityColor();

            // Set the particle colors
            _hitParticles.SetColor(particles.ToUnityColor());
            _sustainParticles.SetColor(particles.ToUnityColor());
        }

        public void SetPressed(bool pressed)
        {
            _innerMaterial.SetFloat(_fade, pressed ? 1f : 0f);
        }

        public void PlayHitAnimation()
        {
            StopAnimation();
            _animation.Play("FretsGuitar");
        }

        public void PlayDrumAnimation()
        {
            StopAnimation();
            _animation.Play("FretsDrums");
        }

        public void PlayHitParticles()
        {
            _hitParticles.Play();
        }

        public void StopAnimation()
        {
            _animation.Stop();
            _animation.Rewind();
        }

        public void SetSustained(bool sustained)
        {
            if (sustained)
            {
                _sustainParticles.Play();
            }
            else
            {
                _sustainParticles.Stop();
            }
        }

        public static void CreateFromThemeFret(ThemeFret themeFret)
        {
            var fretComp = themeFret.gameObject.AddComponent<Fret>();

            fretComp._hitParticles = themeFret.HitEffect;
            fretComp._sustainParticles = themeFret.SustainEffect;

            fretComp._animation = themeFret.Animation;

            fretComp._meshRenderer = themeFret.ColoredMaterialRenderer;
            fretComp._topIndex = themeFret.ColoredMaterialIndex;
            fretComp._innerIndex = themeFret.ColoredInnerMaterialIndex;

            Destroy(themeFret);
        }
    }
}