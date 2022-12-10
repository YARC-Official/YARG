using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Pools;
using YARG.UI;
using YARG.Utils;

namespace YARG {
	public class Track : MonoBehaviour {
		public const float TRACK_SPAWN_OFFSET = 3f;

		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private MeshRenderer trackRenderer;
		[SerializeField]
		private Transform hitWindow;

		[SerializeField]
		private Color[] fretColors;
		[SerializeField]
		private float[] fretPositions;
		[SerializeField]
		private GameObject fret;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;

		private Fret[] frets = null;
		private int visualChartIndex = 0;
		private int realChartIndex = 0;
		private int eventChartIndex = 0;

		private Queue<List<NoteInfo>> expectedHits = new();
		private List<NoteInfo> heldNotes = new();

		private int _combo = 0;
		private int Combo {
			get => _combo;
			set => _combo = value;
		}

		private void Awake() {
			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.DefaultHDR
			);
			var renderTexture = new RenderTexture(descriptor);
			trackCamera.targetTexture = renderTexture;
		}

		private void OnDestroy() {
			// Release render texture
			trackCamera.targetTexture.Release();
		}

		private void Start() {
			// Set render texture
			GameUI.Instance.AddTrackImage(trackCamera.targetTexture);

			// Spawn in frets
			frets = new Fret[5];
			for (int i = 0; i < 5; i++) {
				var fretObj = Instantiate(fret, transform);
				fretObj.transform.localPosition = new Vector3(fretPositions[i], 0.01f, -1.75f);

				var fretComp = fretObj.GetComponent<Fret>();
				fretComp.SetColor(fretColors[i]);
				frets[i] = fretComp;
			}

			// Adjust hit window
			var scale = hitWindow.localScale;
			hitWindow.localScale = new(scale.x, Game.HIT_MARGIN * Game.Instance.SongSpeed * 2f, scale.z);
		}

		private void OnEnable() {
			Game.Instance.FretPressEvent += FretPressAction;
		}

		private void OnDisable() {
			Game.Instance.FretPressEvent -= FretPressAction;
		}

		private void Update() {
			// Update track UV
			var trackMaterial = trackRenderer.material;
			var oldOffset = trackMaterial.GetTextureOffset("_BaseMap");
			float movement = Time.deltaTime * Game.Instance.SongSpeed / 4f;
			trackMaterial.SetTextureOffset("_BaseMap", new(oldOffset.x, oldOffset.y - movement));

			// Update visuals and events
			float relativeTime = Game.Instance.SongTime + ((TRACK_SPAWN_OFFSET + 1.75f) / Game.Instance.SongSpeed);
			var chart = Game.Instance.chart;
			var events = Game.Instance.chartEvents;

			// Since chart is sorted, this is guaranteed to work
			while (chart.Count > visualChartIndex && chart[visualChartIndex].time <= relativeTime) {
				var noteInfo = chart[visualChartIndex];

				SpawnNote(noteInfo, relativeTime);
				visualChartIndex++;
			}

			// Update events
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= relativeTime) {
				var eventInfo = events[eventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(relativeTime, eventInfo.time);
				if (eventInfo.name == "beatLine_minor") {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				} else if (eventInfo.name == "beatLine_major") {
					genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				}

				eventChartIndex++;
			}

			// Update expected input
			while (chart.Count > realChartIndex && chart[realChartIndex].time <= Game.Instance.SongTime + Game.HIT_MARGIN) {
				var noteInfo = chart[realChartIndex];

				var peeked = expectedHits.ReversePeekOrNull();
				if (peeked?[0].time == noteInfo.time) {
					// Add notes as chords
					peeked.Add(noteInfo);
				} else {
					// Or add notes as singular
					var l = new List<NoteInfo>(5) { noteInfo };
					expectedHits.Enqueue(l);
				}

				realChartIndex++;
			}

			// Update real input
			UpdateInput();

			// Update held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];
				if (heldNote.time + heldNote.length <= Game.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Game.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Game.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					notePool.MissNote(hit);
				}
			}

			if (expectedHits.Count <= 0) {
				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();
			if (!chord[0].hopo && !Game.Instance.StrumThisFrame) {
				return;
			} else if (chord[0].hopo && Combo <= 0 && !Game.Instance.StrumThisFrame) {
				return;
			}

			// Convert NoteInfo list to chord fret array
			int[] chordInts = new int[chord.Count];
			for (int i = 0; i < chordInts.Length; i++) {
				chordInts[i] = chord[i].fret;
			}

			// Check if correct chord is pressed
			if (!ChordPressed(chordInts)) {
				if (!chord[0].hopo) {
					Combo = 0;
				}

				return;
			}

			// If so, hit!
			expectedHits.Dequeue();

			Combo++;
			foreach (var hit in chord) {
				// Hit notes
				notePool.HitNote(hit);

				// Play particles
				frets[hit.fret].PlayParticles();

				// If sustained, add to held
				if (hit.length > 0.2f) {
					heldNotes.Add(hit);
					frets[hit.fret].PlaySustainParticles();
				}
			}
		}

		private bool ChordPressed(int[] chord) {
			if (chord.Length == 1) {
				// Deal with single notes
				int fret = chord[0];
				for (int i = 0; i < frets.Length; i++) {
					if (frets[i].IsPressed && i > fret) {
						return false;
					} else if (!frets[i].IsPressed && i == fret) {
						return false;
					} else if (frets[i].IsPressed && i != fret && !Game.ANCHORING) {
						return false;
					}
				}
			} else {
				// Deal with multi-key chords
				for (int i = 0; i < frets.Length; i++) {
					bool contains = chord.Contains(i);
					if (contains && !frets[i].IsPressed) {
						return false;
					} else if (!contains && frets[i].IsPressed) {
						return false;
					}
				}
			}

			return true;
		}

		private void FretPressAction(bool on, int fret) {
			frets[fret].SetPressed(on);

			if (!on) {
				for (int i = heldNotes.Count - 1; i >= 0; i--) {
					var heldNote = heldNotes[i];
					if (heldNote.fret != fret) {
						continue;
					}

					notePool.MissNote(heldNote);
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			var pos = new Vector3(fretPositions[noteInfo.fret], 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			var noteComp = notePool.CreateNote(noteInfo, pos);
			noteComp.SetInfo(fretColors[noteInfo.fret], noteInfo.length, noteInfo.hopo);
		}

		private float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * Game.Instance.SongSpeed;
		}
	}
}