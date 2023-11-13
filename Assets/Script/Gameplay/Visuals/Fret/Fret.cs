using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Helpers;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class Fret : MonoBehaviour
    {
        private static readonly int _fade = Shader.PropertyToID("Fade");

        [SerializeField]
        private ParticleGroup _hitParticles;
        [SerializeField]
        private ParticleGroup _sustainParticles;

        [Space]
        [SerializeField]
        private Animation _animation;

        [Space]
        [SerializeField]
        private MeshRenderer _fretMesh;
        [SerializeField]
        private int _topIndex;
        [SerializeField]
        private int _innerIndex;

        private Material _innerMaterial;

        public void Initialize(Color top, Color inner, Color particles)
        {
            var topMaterial = _fretMesh.materials[_topIndex];
            topMaterial.color = top.ToUnityColor();
            topMaterial.SetColor("_EmissionColor", top.ToUnityColor() * 11.5f);

            _innerMaterial = _fretMesh.materials[_innerIndex];
            _innerMaterial.color = inner.ToUnityColor();

            _hitParticles.Colorize(particles.ToUnityColor());
            _sustainParticles.Colorize(particles.ToUnityColor());
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
    }
}