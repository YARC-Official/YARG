using TMPro;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class NoteComponent : Poolable {
		public enum ModelType {
			NOTE,
			HOPO,
			TAP,
			FULL,
			FULL_HOPO
		}

		private enum State {
			WAITING,
			HITTING,
			MISSED
		}

		[SerializeField]
		private MeshRenderer[] meshRenderers;
		[SerializeField]
		private int[] meshRendererMiddleIndices;
		[SerializeField]
		private float[] noteGroupsEmission;
		[SerializeField]
		private bool[] boostColor;

		[Space]
		[SerializeField]
		private GameObject noteGroup;
		[SerializeField]
		private GameObject hopoGroup;
		[SerializeField]
		private GameObject tapGroup;
		[SerializeField]
		private GameObject fullGroup;
		[SerializeField]
		private GameObject fullHopoGroup;

		[Space]
		[SerializeField]
		private TextMeshPro fretNumber;
		[SerializeField]
		private LineRenderer lineRenderer;

		private float _brutalVanishDistance;
		/// <summary>
		/// Ranges between -1 and 1. Notes will disappear when they reach this percentage down the track.
		/// </summary>
		/// <remarks>
		/// A value of 0 is the strikeline, while a value of 1 is the top of the track.<br/>
		/// Larger numbers will cause the notes to disappear sooner.<br/>
		/// Notes will not disappear at all if this number is below 0.
		/// </remarks>
		private float BrutalVanishDistance {
			get => _brutalVanishDistance;
			set {
				_brutalVanishDistance = System.Math.Clamp(value, -1, 1);
			}
		}

		private bool BrutalIsNoteVanished {
			get => PercentDistanceFromStrikeline <= BrutalVanishDistance && state == State.WAITING;
		}

		private float PercentDistanceFromStrikeline {
			get {
				const float TRACK_START = 3.00f;
				const float TRACK_END = -1.76f;
				const float range = TRACK_START - TRACK_END;

				var result = (transform.position.z - TRACK_END) / range;
				return result;
			}
		}

		private Color _colorCacheNotes = Color.white;
		private Color ColorCacheNotes {
			get {
				if (isActivatorNote) {
					return Color.magenta;
				}

				// If within starpower section
				if (pool.player.track.StarpowerSection?.EndTime > pool.player.track.RelativeTime) {
					return Color.white;
				}

				return _colorCacheNotes;
			}
			set => _colorCacheNotes = value;
		}

		private Color _colorCacheSustains = Color.white;
		private Color ColorCacheSustains {
			get {
				if (BrutalIsNoteVanished) {
					return Color.clear;
				}

				// If within starpower section
				if (pool.player.track.StarpowerSection?.EndTime > pool.player.track.RelativeTime) {
					return Color.white;
				}

				return _colorCacheSustains;
			}
			set => _colorCacheSustains = value;
		}

		private float lengthCache = 0f;

		private State state = State.WAITING;

		private bool isActivatorNote;

		private void OnEnable() {
			if (pool != null) {
				pool.player.track.StarpowerMissEvent += UpdateColor;
			}

			foreach (MeshRenderer r in meshRenderers) {
				r.enabled = true;
			}
			lineRenderer.enabled = true;
		}

		private void OnDisable() {
			if (pool != null) {
				pool.player.track.StarpowerMissEvent -= UpdateColor;
			}
		}

		public void SetInfo(Color notes, Color sustains, float length, ModelType type, bool isDrumActivator = false) {
			static void SetModelActive(GameObject obj, ModelType inType, ModelType needType) {
				if (obj != null) {
					obj.SetActive(inType == needType);
				}
			}

			// Show/hide models
			SetModelActive(noteGroup, type, ModelType.NOTE);
			SetModelActive(hopoGroup, type, ModelType.HOPO);
			SetModelActive(tapGroup, type, ModelType.TAP);
			SetModelActive(fullGroup, type, ModelType.FULL);
			SetModelActive(fullHopoGroup, type, ModelType.FULL_HOPO);

			SetLength(length);

			state = State.WAITING;
			ColorCacheNotes = notes;
			ColorCacheSustains = sustains;

			isActivatorNote = isDrumActivator;

			UpdateColor();
			UpdateRandomness();
			ResetLineAmplitude();
		}

		public void SetFretNumber(string str) {
			fretNumber.gameObject.SetActive(true);
			fretNumber.text = str;
		}

		private void UpdateColor() {
			for (int i = 0; i < meshRenderers.Length; i++) {
				int index = meshRendererMiddleIndices[i];

				if (boostColor[i]) {
					meshRenderers[i].materials[index].color = ColorCacheNotes + new Color(3, 3, 3, 0);
				} else {
					meshRenderers[i].materials[index].color = ColorCacheNotes;
				}

				meshRenderers[i].materials[index].SetColor("_EmissionColor", ColorCacheNotes * noteGroupsEmission[i]);
			}

			UpdateLineColor();
		}

		private void UpdateRandomness() {
			for (int i = 0; i < meshRenderers.Length; i++) {
				int index = meshRendererMiddleIndices[i];
				var material = meshRenderers[i].materials[index];

				if (material.HasFloat("_RandomFloat")) {
					material.SetFloat("_RandomFloat", Random.Range(-1f, 1f));
				}

				if (material.HasVector("_RandomVector")) {
					material.SetVector("_RandomVector", new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
				}
			}
		}

		private void UpdateLineColor() {
			if (lengthCache == 0f) {
				return;
			}

			var mat = lineRenderer.materials[0];
			if (state == State.WAITING) {
				mat.color = ColorCacheSustains;
				mat.SetColor("_EmissionColor", ColorCacheSustains);
			} else if (state == State.HITTING) {
				mat.color = ColorCacheSustains;
				mat.SetColor("_EmissionColor", ColorCacheSustains * 3f);
			} else if (state == State.MISSED) {
				mat.color = new(0.9f, 0.9f, 0.9f, 0.5f);
				mat.SetColor("_EmissionColor", Color.black);
			}
		}

		private void ResetLineAmplitude() {
			lineRenderer.materials[0].SetFloat("_PrimaryAmplitude", 0f);
			lineRenderer.materials[0].SetFloat("_SecondaryAmplitude", 0f);
			lineRenderer.materials[0].SetFloat("_TertiaryAmplitude", 0f);
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
			static void Hide(GameObject obj) {
				if (obj != null) {
					obj.SetActive(false);
				}
			}

			Hide(noteGroup);
			Hide(hopoGroup);
			Hide(tapGroup);
			Hide(fullGroup);
			Hide(fullHopoGroup);

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
			ResetLineAmplitude();
		}

		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * pool.player.trackSpeed);
			if (state == State.HITTING) {
				// Get the new line start position. Said position should be at
				// the fret board and relative to the note itelf.
				float newStart = -transform.localPosition.z - AbstractTrack.TRACK_END_OFFSET;

				// Apply to line renderer
				lineRenderer.SetPosition(1, new(0f, 0f, newStart));
			}

			// Remove if off screen
			if (transform.localPosition.z < -3f - lengthCache) {
				MoveToPool();
			}

			// Line hit animation
			if (state == State.HITTING) {
				// Change line amplitude
				var lineMat = lineRenderer.materials[0];
				lineMat.SetFloat("_PrimaryAmplitude", 0.38f);
				lineMat.SetFloat("_SecondaryAmplitude", Mathf.Sin(Time.time * 4f));
				lineMat.SetFloat("_TertiaryAmplitude", Mathf.Sin(Time.time * 1.7f)*0.222f);

				// Move line forward
				float forwardSub = Time.deltaTime * pool.player.trackSpeed / 2.5f;
				lineMat.SetFloat("_ForwardOffset", lineMat.GetFloat("_ForwardOffset") + forwardSub);
			}

			float multiplier = pool.player.track.Multiplier;
			float maxMultiplier = pool.player.track.MaxMultiplier;

			// TODO: If/when health system gets added, this should use that instead. Multiplier isn't a good way to scale difficulty here.
			if (pool.player.brutalMode) {
				BrutalVanishDistance = System.Math.Min(
					System.Math.Max(
						0.25f, multiplier / maxMultiplier
					),
					0.80f
				);
			} else {
				BrutalVanishDistance = -1.0f;
			}

			BrutalUpdateNoteVanish();
		}

		private void BrutalUpdateNoteVanish() {
			if (BrutalIsNoteVanished) {
				foreach (MeshRenderer r in meshRenderers) {
					r.enabled = false;
				}
				UpdateLineColor();
			} else {
				foreach (MeshRenderer r in meshRenderers) {
					r.enabled = true;
				}
				UpdateLineColor();
			}
		}
	}
}