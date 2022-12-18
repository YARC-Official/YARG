using UnityEngine;

namespace YARG {
	public class NoteComponent : MonoBehaviour {
		[HideInInspector]
		public NotePool notePool;

		[SerializeField]
		private GameObject noteGroup;
		[SerializeField]
		private GameObject hopoGroup;
		[SerializeField]
		private MeshRenderer[] meshRenderers;
		[SerializeField]
		private LineRenderer lineRenderer;

		private Color colorCache = Color.white;
		private float lengthCache = 0f;

		private bool isHitting = false;

		public void SetInfo(Color c, float length, bool hopo) {
			noteGroup.SetActive(!hopo);
			hopoGroup.SetActive(hopo);
			isHitting = false;

			SetColor(c);
			SetLength(length);
		}

		private void SetColor(Color c) {
			colorCache = c;

			foreach (var meshRenderer in meshRenderers) {
				meshRenderer.materials[0].color = c;
			}

			lineRenderer.materials[0].color = c;
			lineRenderer.materials[0].SetColor("_EmissionColor", c);
		}

		private void SetLength(float length) {
			if (length <= 0.2f) {
				lineRenderer.enabled = false;
				lengthCache = 0f;
				return;
			}

			length *= notePool.player.trackSpeed;
			lengthCache = length;

			lineRenderer.enabled = true;
			lineRenderer.SetPosition(0, new(0f, 0.01f, length));
			lineRenderer.SetPosition(1, Vector3.zero);
		}

		public void HitNote() {
			noteGroup.SetActive(false);
			hopoGroup.SetActive(false);
			isHitting = true;

			// Update line color
			if (lengthCache != 0f) {
				lineRenderer.materials[0].SetColor("_EmissionColor", colorCache * 8f);
			}
		}

		public void MissNote() {
			isHitting = false;

			// Update line color
			if (lengthCache != 0f) {
				lineRenderer.materials[0].color = new(0.9f, 0.9f, 0.9f, 0.5f);
				lineRenderer.materials[0].SetColor("_EmissionColor", Color.black);
			}
		}

		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * notePool.player.trackSpeed);

			if (isHitting) {
				// Get the new line start position. Said position should be at
				// the fret board (-1.75f) and relative to the note itelf.
				float newStart = -transform.localPosition.z - 1.75f;

				// Apply to line renderer
				lineRenderer.SetPosition(1, new(0f, 0f, newStart));
			}

			if (transform.localPosition.z < -3f - lengthCache) {
				notePool.RemoveNote(this);
			}
		}
	}
}