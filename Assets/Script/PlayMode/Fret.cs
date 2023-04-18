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

		[SerializeField]
		private int topMaterialIndex;
		[SerializeField]
		private int innerMaterialIndex;

		/// <value>
		/// Whether or not the fret is pressed. Used for data purposes.
		/// </value>
		public bool IsPressed {
			get;
			private set;
		} = false;

		public void SetColor(Color top, Color inner) {
			meshRenderer.materials[topMaterialIndex].color = top;
			meshRenderer.materials[topMaterialIndex].SetColor("_EmissionColor", top * 11.5f);
			meshRenderer.materials[innerMaterialIndex].color = inner;


			hitParticles.Colorize(inner);
			sustainParticles.Colorize(inner);
		}

		public void SetPressed(bool pressed) {
			meshRenderer.materials[innerMaterialIndex].SetFloat("Fade", pressed ? 1f : 0f);

			IsPressed = pressed;
		}

		public void Pulse() {
			meshRenderer.materials[innerMaterialIndex].SetFloat("Fade", 1f);
		}

		private void Update() {
			if (!fadeOut) {
				return;
			}

			var mat = meshRenderer.materials[innerMaterialIndex];
			float fade = mat.GetFloat("Fade") - Time.deltaTime * 4f;
			mat.SetFloat("Fade", Mathf.Max(fade, 0f));
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