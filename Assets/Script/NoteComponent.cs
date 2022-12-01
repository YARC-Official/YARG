using UnityEngine;

namespace YARG {
	public class NoteComponent : MonoBehaviour {
		[HideInInspector]
		public NotePool notePool;

		[SerializeField]
		private MeshRenderer meshRenderer;
		[SerializeField]
		private LineRenderer lineRenderer;

		public void SetColor(Color c) {
			meshRenderer.materials[0].color = c;

			lineRenderer.materials[0].color = c;
			lineRenderer.materials[0].SetColor("_EmissionColor", c);
		}

		public void SetLength(float length) {
			if (length <= 0.2f) {
				lineRenderer.enabled = false;
				return;
			}

			lineRenderer.enabled = true;
			lineRenderer.SetPosition(1, new(0f, 0.01f, length * Game.Instance.SongSpeed));
		}

		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * Game.Instance.SongSpeed);

			if (transform.localPosition.z < -3f) {
				notePool.RemoveNote(this);
			}
		}
	}
}