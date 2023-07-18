using UnityEngine;
using YARG.Util;

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
            topMaterial.color = top;
            topMaterial.SetColor("_EmissionColor", top * 11.5f);

            _innerMaterial = _fretMesh.materials[_innerIndex];
            _innerMaterial.color = inner;

            _hitParticles.Colorize(particles);
            _sustainParticles.Colorize(particles);
        }

        public void SetPressed(bool pressed)
        {
            _innerMaterial.SetFloat(_fade, pressed ? 1f : 0f);
        }
    }
}