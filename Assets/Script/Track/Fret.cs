using UnityEngine;
using UnityEngine.Serialization;
using YARG.Util;

namespace YARG.PlayMode
{
    public class Fret : MonoBehaviour
    {
        [SerializeField]
        private bool _fadeOut;

        [SerializeField]
        private ParticleGroup _hitParticles;

        [SerializeField]
        private ParticleGroup _sustainParticles;

        [SerializeField]
        private Animation _animation;

        [SerializeField]
        private MeshRenderer _meshRenderer;

        [SerializeField]
        private int _topMaterialIndex;

        [SerializeField]
        private int _innerMaterialIndex;

        /// <value>
        /// Whether or not the fret is pressed. Used for data purposes.
        /// </value>
        public bool IsPressed { get; private set; }

        public void SetColor(Color top, Color inner, Color particles)
        {
            _meshRenderer.materials[_topMaterialIndex].color = top;
            _meshRenderer.materials[_topMaterialIndex].SetColor("_EmissionColor", top * 11.5f);
            _meshRenderer.materials[_innerMaterialIndex].color = inner;

            _hitParticles.Colorize(particles);
            _sustainParticles.Colorize(particles);
        }

        public void SetPressed(bool pressed)
        {
            _meshRenderer.materials[_innerMaterialIndex].SetFloat("Fade", pressed ? 1f : 0f);

            IsPressed = pressed;
        }

        public void Pulse()
        {
            _meshRenderer.materials[_innerMaterialIndex].SetFloat("Fade", 1f);
        }

        private void Update()
        {
            if (!_fadeOut)
            {
                return;
            }

            var mat = _meshRenderer.materials[_innerMaterialIndex];
            float fade = mat.GetFloat("Fade") - Time.deltaTime * 4f;
            mat.SetFloat("Fade", Mathf.Max(fade, 0f));
        }

        public void PlayParticles()
        {
            _hitParticles.Play();
        }

        public void PlaySustainParticles()
        {
            _sustainParticles.Play();
        }

        public void PlayAnimation()
        {
            StopAnimation();

            _animation.Play("FretsGuitar");
        }

        public void PlayAnimationDrums()
        {
            StopAnimation();

            _animation.Play("FretsDrums");
        }

        public void PlayAnimationDrumsHighBounce()
        {
            StopAnimation();

            _animation.Play("FretsDrumsHighBounce");
        }

        public void PlayAnimationSustainsLooped()
        {
            StopAnimation();

            _animation.Play("FretsGuitarSustains");
        }

        public void StopAnimation()
        {
            _animation.Stop();
            _animation.Rewind();
        }

        public void StopSustainParticles()
        {
            _sustainParticles.Stop();
        }
    }
}