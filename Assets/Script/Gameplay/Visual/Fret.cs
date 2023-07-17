using UnityEngine;
using YARG.Util;

namespace YARG.Gameplay
{
    public class Fret : MonoBehaviour
    {
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

        public void Initialize(Color top, Color inner, Color particles)
        {
            var topMat = _fretMesh.materials[_topIndex];
            topMat.color = top;
            topMat.SetColor("_EmissionColor", top * 11.5f);

            var innerMat = _fretMesh.materials[_innerIndex];
            innerMat.color = inner;

            _hitParticles.Colorize(particles);
            _sustainParticles.Colorize(particles);
        }
    }
}