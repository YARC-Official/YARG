using UnityEngine;
using YARG.Utils;

namespace YARG {
	public class Fret : MonoBehaviour {
		[SerializeField]
		private Material pressedMaterial;
		[SerializeField]
		private Material releasedMaterial;
		[SerializeField]
		private ParticleGroup hitParticles;
		[SerializeField]
		private ParticleGroup sustainParticles;

		[SerializeField]
		private MeshRenderer meshRenderer;

		private Color color;

		public bool IsPressed {
			get;
			private set;
		} = false;

		public void SetColor(Color c) {
			color = c;
			meshRenderer.material.color = c;
			hitParticles.Colorize(c);
			sustainParticles.Colorize(c);
		}

		public void SetPressed(bool pressed) {
			meshRenderer.material = pressed ? pressedMaterial : releasedMaterial;
			meshRenderer.material.color = color;

			IsPressed = pressed;
		}

		public void PlayParticles() {
			hitParticles.Play();
		}

		public void PlaySustainParticles() {
			sustainParticles.Play();
		}

		public void StopSustainParticles() {
			sustainParticles.Stop();
		}
	}
}