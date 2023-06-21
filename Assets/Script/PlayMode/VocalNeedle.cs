using UnityEngine;
using YARG.Util;

namespace YARG.PlayMode {
	public class VocalNeedle : MonoBehaviour {
		public MeshRenderer meshRenderer;
		public ParticleGroup nonActiveParticles;
		public ParticleGroup activeParticles;
		public Light needleLight;

		[SerializeField]
		private Texture2D[] needleTextures;

		private float _particleSpeed;
		public float ParticleSpeed {
			get {
				return _particleSpeed;
			}
			set {
				_particleSpeed = value;
				activeParticles.SetStartSpeed(value);
				nonActiveParticles.SetStartSpeed(value);
			}
		}

		private void Start() {
			// Give random needle (for now)
			meshRenderer.material.mainTexture = needleTextures[Random.Range(0, needleTextures.Length)];
		}
	}
}