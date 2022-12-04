using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Pools;

namespace YARG {
	public class Track : MonoBehaviour {
		public const float TRACK_SPAWN_OFFSET = 3f;

		[SerializeField]
		private MeshRenderer trackRenderer;

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

		private SortedDictionary<float, List<NoteInfo>> expectedHits = new();
		private List<NoteInfo> heldNotes = new();

		private void Start() {
			// Spawn in frets
			frets = new Fret[5];
			for (int i = 0; i < 5; i++) {
				var fretObj = Instantiate(fret, transform);
				fretObj.transform.localPosition = new Vector3(fretPositions[i], 0.01f, -1.75f);

				var fretComp = fretObj.GetComponent<Fret>();
				fretComp.SetColor(fretColors[i]);
				frets[i] = fretComp;
			}
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

				if (eventInfo.name == "beatLine_minor") {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, TRACK_SPAWN_OFFSET));
				} else if (eventInfo.name == "beatLine_major") {
					genericPool.Add("beatLine_major", new(0f, 0.01f, TRACK_SPAWN_OFFSET));
				}

				eventChartIndex++;
			}

			// Update expected input
			while (chart.Count > realChartIndex && chart[realChartIndex].time <= Game.Instance.SongTime + Game.HIT_MARGIN) {
				var noteInfo = chart[realChartIndex];

				// Add notes at chords
				if (expectedHits.TryGetValue(noteInfo.time, out var list)) {
					list.Add(noteInfo);
				} else {
					var l = new List<NoteInfo>(5) { noteInfo };
					expectedHits.Add(noteInfo.time, l);
				}
				realChartIndex++;
			}

			// Update real input
			foreach (var kv in expectedHits.ToArray()) {
				var chord = kv.Value;

				// Handle misses
				if (Game.Instance.SongTime - chord[0].time > Game.HIT_MARGIN) {
					expectedHits.Remove(chord[0].time);

					// Call miss for each component
					foreach (var hit in chord) {
						notePool.MissNote(hit);
					}
				}

				// Handle hits
				if (Game.Instance.StrumThisFrame) {
					// Convert NoteInfo list to chord fret array
					int[] chordInts = new int[chord.Count];
					for (int i = 0; i < chordInts.Length; i++) {
						chordInts[i] = chord[i].fret;
					}

					// Check if correct chord is pressed
					if (!ChordPressed(chordInts)) {
						continue;
					}

					// If so, hit!
					expectedHits.Remove(chord[0].time);

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

					// Only hit one note per frame
					break;
				}
			}

			// Update held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];
				if (heldNote.time + heldNote.length <= Game.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}
		}

		private bool ChordPressed(int[] chord) {
			for (int i = 0; i < frets.Length; i++) {
				if (chord.Contains(i)) {
					if (!frets[i].IsPressed) {
						return false;
					}
				} else {
					if (frets[i].IsPressed) {
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
			float lagCompensation = (time - noteInfo.time) * Game.Instance.SongSpeed;
			var pos = new Vector3(fretPositions[noteInfo.fret], 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			var noteComp = notePool.CreateNote(noteInfo, pos);
			noteComp.SetInfo(fretColors[noteInfo.fret], noteInfo.length, noteInfo.hopo);
		}
	}
}