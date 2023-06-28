using UnityEngine;
using YARG.Util;

namespace YARG.PlayMode
{
    public class VocalNeedle : MonoBehaviour
    {
        public MeshRenderer meshRenderer;
        public ParticleGroup nonActiveParticles;
        public ParticleGroup activeParticles;
        public Light needleLight;

        [SerializeField]
        private Texture2D[] needleTextures;

        public void SetLineProperties(float lifetime, float startSpeed, float emissionRate)
        {
            activeParticles.SetStartLifetime(lifetime);
            activeParticles.SetStartSpeed(startSpeed);
            activeParticles.SetEmissionRate(emissionRate);
            nonActiveParticles.SetStartLifetime(lifetime);
            nonActiveParticles.SetStartSpeed(startSpeed);
            nonActiveParticles.SetEmissionRate(emissionRate);
        }

        private void Start()
        {
            // Give random needle (for now)
            meshRenderer.material.mainTexture = needleTextures[Random.Range(0, needleTextures.Length)];
        }
    }
}