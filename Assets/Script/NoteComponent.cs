using UnityEngine;

namespace YARG {
	public class NoteComponent : MonoBehaviour {
		[HideInInspector]
		public NotePool notePool;

		[SerializeField]
		private GameObject noteGroup;
		[SerializeField]
		private MeshRenderer meshRenderer;
		[SerializeField]
		private LineRenderer lineRenderer;

		private float lengthCache = 0f;
		private bool isHitting = false;

		public void SetInfo(Color c, float length) {
			noteGroup.SetActive(true);
			isHitting = false;

			SetColor(c);
			SetLength(length);
		}

		private void SetColor(Color c) {
			meshRenderer.materials[0].color = c;

			lineRenderer.materials[0].color = c;
			lineRenderer.materials[0].SetColor("_EmissionColor", c);
		}

		private void SetLength(float length) {
			if (length <= 0.2f) {
				lineRenderer.enabled = false;
				lengthCache = 0f;
				return;
			}

			length *= Game.Instance.SongSpeed;
			lengthCache = length;

			lineRenderer.enabled = true;
			lineRenderer.SetPosition(0, Vector3.zero);
			lineRenderer.SetPosition(1, new(0f, 0.01f, length));
		}

		public void HitNote() {
			noteGroup.SetActive(false);
			isHitting = true;
		}

		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * Game.Instance.SongSpeed);

			if (isHitting) {
				// Get the new line start position. Said position should be at
				// the fret board (-1.75f) and relative to the note itelf.
				float newStart = -transform.localPosition.z - 1.75f;

				// Apply to line renderer
				lineRenderer.SetPosition(0, new(0f, 0f, newStart));
			}

			if (transform.localPosition.z < -3f - lengthCache) {
				notePool.RemoveNote(this);
			}
		}
	}
}