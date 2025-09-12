using UnityEngine;

namespace YARG.Venue.Stage
{

    [RequireComponent(typeof(ParticleSystem))]
    public class StageElement : MonoBehaviour
    {
        [SerializeField]
        public StageElementType ElementType;

        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void StartEffect()
        {
            if (_particleSystem == null)
            {
                return;
            }

            _particleSystem.Play(true);
        }

        public void StopEffect()
        {
            if (_particleSystem == null)
            {
                return;
            }

            _particleSystem.Stop(true);
        }
    }

    public enum StageElementType
    {
        Pyro,
        Fog
    }
}