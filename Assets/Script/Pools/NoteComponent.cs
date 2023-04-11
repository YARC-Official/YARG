using TMPro;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class NoteComponent : Poolable {
		public enum ModelType {
			NOTE,
			HOPO,
			FULL
		}

		private enum State {
			WAITING,
			HITTING,
			MISSED
		}

		[SerializeField]
		private GameObject noteGroup;
		[SerializeField]
		private GameObject hopoGroup;
		[SerializeField]
		private GameObject fullGroup;
		[SerializeField]
		private MeshRenderer[] meshRenderers;
		[SerializeField]
		private TextMeshPro fretNumber;
		[SerializeField]
		private LineRenderer lineRenderer;

		private Color _colorCache = Color.white;
		private Color ColorCache {
			get {
				// If within starpower section
				if (pool.player.track.StarpowerSection?.EndTime > pool.player.track.RelativeTime) {
					return Color.white;
				}

				return _colorCache;
			}
			set => _colorCache = value;
		}

		private float lengthCache = 0f;

		private State state = State.WAITING;

		private void OnEnable() {
			if (pool != null) {
				pool.player.track.StarpowerMissEvent += UpdateColor;
			}
		}

		private void OnDisable() {
			if (pool != null) {
				pool.player.track.StarpowerMissEvent -= UpdateColor;
			}
		}

		public void SetInfo(Color c, float length, ModelType hopo) {
			noteGroup.SetActive(hopo == ModelType.NOTE);
			hopoGroup.SetActive(hopo == ModelType.HOPO);
			fullGroup.SetActive(hopo == ModelType.FULL);

			state = State.WAITING;

			SetLength(length);

			ColorCache = c;
			UpdateColor();
		}

		public void SetFretNumber(string str) {
			fretNumber.gameObject.SetActive(true);
			fretNumber.text = str;
		}

		private void UpdateColor() {
			foreach (var meshRenderer in meshRenderers) {
				if (meshRenderer.name == "Model") {
					// Apply the different value for the specific MeshRenderer
					meshRenderer.materials[0].color = ColorCache;
				} else {
					meshRenderer.materials[1].color = ColorCache;
				}
			}

			UpdateLineColor();
		}

		private void UpdateLineColor() {
			if (lengthCache == 0f) {
				return;
			}

			if (state == State.WAITING) {
				lineRenderer.materials[0].color = ColorCache;
				lineRenderer.materials[0].SetColor("_EmissionColor", ColorCache);
			} else if (state == State.HITTING) {
				lineRenderer.materials[0].color = ColorCache;
				lineRenderer.materials[0].SetColor("_EmissionColor", ColorCache * 8f);
			} else if (state == State.MISSED) {
				lineRenderer.materials[0].color = new(0.9f, 0.9f, 0.9f, 0.5f);
				lineRenderer.materials[0].SetColor("_EmissionColor", Color.black);
			}
		}

		private void SetLength(float length) {
			if (length <= 0.2f) {
				lineRenderer.enabled = false;
				lengthCache = 0f;
				return;
			}

			length *= pool.player.trackSpeed / Play.speed;
			lengthCache = length;

			lineRenderer.enabled = true;
			lineRenderer.SetPosition(0, new(0f, 0.01f, length));
			lineRenderer.SetPosition(1, Vector3.zero);
		}

		public void HitNote() {
			noteGroup.SetActive(false);
			hopoGroup.SetActive(false);
			fullGroup.SetActive(false);

			if (fretNumber != null) {
				fretNumber.gameObject.SetActive(false);
			}

			state = State.HITTING;
			UpdateLineColor();
		}

		public void MissNote() {
			if (fretNumber != null) {
				fretNumber.gameObject.SetActive(false);
			}

			state = State.MISSED;
			UpdateLineColor();
		}

		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * pool.player.trackSpeed);

			if (state == State.HITTING) {
				// Get the new line start position. Said position should be at
				// the fret board (-1.75f) and relative to the note itelf.
				float newStart = -transform.localPosition.z - 1.75f;

				// Apply to line renderer
				lineRenderer.SetPosition(1, new(0f, 0f, newStart));
			}

			if (transform.localPosition.z < -3f - lengthCache) {
				MoveToPool();
			}
		}
	}
}