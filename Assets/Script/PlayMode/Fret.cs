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
		private new Animation animation;

		[SerializeField]
		private MeshRenderer meshRenderer;

		[SerializeField]
		private int topMaterialIndex;
		[SerializeField]
		private int innerMaterialIndex;

		[SerializeField]
		private Transform fretItself;
		public Vector3 fretInitialScale;
		public float scaleY;

		/// <value>
		/// Whether or not the fret is pressed. Used for data purposes.
		/// </value>
		public bool IsPressed {
			get;
			private set;
		} = false;

		void Start() {
			//fretItself = transform.GetComponent<Fret>();
			fretInitialScale = fretItself.transform.localScale;

			scaleY = fretInitialScale.y;
			Debug.Log("y: " + scaleY);

		}

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

		public void PlayAnimation() {
			animation["FretsGuitar"].wrapMode = WrapMode.Once;
			animation.Play("FretsGuitar");
		}

		public void PlayAnimationSustainsLooped() {
			animation["FretsGuitarSustains"].wrapMode = WrapMode.Loop;
			animation.Play("FretsGuitarSustains");
		}

		public void StopAnimation() {
			//animation["FretsGuitar"].wrapMode = WrapMode.Once;

			animation.Stop("FretsGuitar");
			fretItself.transform.localScale = fretInitialScale + new Vector3(0f, 0, -0.009f);
		}

		public void StopAnimationSustains() {
			//animation["FretsGuitar"].wrapMode = WrapMode.Once;

			animation.Play("FretsGuitar");
			animation.Stop("FretsGuitar");
			fretItself.transform.localScale = fretInitialScale + new Vector3(0f, 0, +0.005682f);
		}

		public void StopSustainParticles() {
			sustainParticles.Stop();
		}
	}
}