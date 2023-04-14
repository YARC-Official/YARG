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

		public Material[] guitarMaterials;
		public Material[] drumMaterials;
		
		/// <value>
		/// Whether or not the fret is pressed. Used for data purposes.
		/// </value>
		public bool IsPressed {
			get;
			private set;
		} = false;

		public void SetColor(bool isGuitar, int c) {
			if (isGuitar) {
				meshRenderer.materials[1] = guitarMaterials[c];
			}
		}

		public void SetPressed(bool pressed) {
			//meshRenderer.material.;

			IsPressed = pressed;
		}

		public void Pulse() {
			meshRenderer.material.SetFloat("Fade", 1f);
		}

		private void Update() {
			if (!fadeOut) {
				return;
			}

			float fade = meshRenderer.materials[1].GetFloat("Fade") - Time.deltaTime * 4f;
			meshRenderer.materials[1].SetFloat("Fade", Mathf.Max(fade, 0f));
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