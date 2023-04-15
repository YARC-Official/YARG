using UnityEngine;
using YARG.Util;

namespace YARG.PlayMode {
	public class Fret : MonoBehaviour {
		[SerializeField]
		private bool fadeOut;

		[SerializeField]
		private ParticleGroup hitParticles;
		[SerializeField]
		private ParticleGroup sustainParticles;

		[SerializeField]
		private MeshRenderer meshRenderer;

		/// <value>
		/// Whether or not the fret is pressed. Used for data purposes.
		/// </value>
		public bool IsPressed {
			get;
			private set;
		} = false;

		public void SetColor(Color c) {
			meshRenderer.material.color = c;
			meshRenderer.material.SetColor("_EmissionColor", c * Mathf.LinearToGammaSpace(2f));
			hitParticles.Colorize(c);
			sustainParticles.Colorize(c);
		}

		public void SetPressed(bool pressed) {
			meshRenderer.material.SetFloat("Fade", pressed ? 1f : 0f);

			IsPressed = pressed;
		}

		public void Pulse() {
			meshRenderer.material.SetFloat("Fade", 1f);
		}

		private void Update() {
			if (!fadeOut) {
				return;
			}

			float fade = meshRenderer.material.GetFloat("Fade") - Time.deltaTime * 4f;
			meshRenderer.material.SetFloat("Fade", Mathf.Max(fade, 0f));
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