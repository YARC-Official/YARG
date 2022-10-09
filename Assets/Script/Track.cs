using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Track : MonoBehaviour {
	[SerializeField]
	private MeshFilter meshFilter;

	[SerializeField]
	private Color[] fretColors;
	[SerializeField]
	private float[] fretPositions;
	[SerializeField]
	private GameObject fret;
	[SerializeField]
	private GameObject note;
	[SerializeField]
	private GameObject hitParticles;

	private Fret[] frets = null;
	private int visualChartIndex = 0;
	private int realChartIndex = 0;

	private Dictionary<NoteInfo, Note> spawnedNotes = new();
	private Dictionary<float, List<NoteInfo>> expectedHits = new();

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

		// Sort by time
		Game.Instance.chart.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
	}

	private void OnEnable() {
		Game.Instance.FretPressEvent += FretPressAction;
	}

	private void OnDisable() {
		Game.Instance.FretPressEvent -= FretPressAction;
	}

	private void Update() {
		// Update track UV
		var uvs = meshFilter.mesh.uv;
		for (int i = 0; i < uvs.Length; i++) {
			uvs[i] += new Vector2(0f, Time.deltaTime * Game.Instance.songSpeed);
		}
		meshFilter.mesh.uv = uvs;

		// Update visuals
		float relativeTime = Game.Instance.songTime + (3.75f / Game.Instance.songSpeed);
		var chart = Game.Instance.chart;

		// Since chart is sorted, this is guaranteed to work
		while (chart.Count > visualChartIndex && chart[visualChartIndex].time <= relativeTime) {
			var noteInfo = chart[visualChartIndex];

			SpawnNote(noteInfo);
			visualChartIndex++;
		}

		// Update expected input
		while (chart.Count > realChartIndex && chart[realChartIndex].time <= Game.Instance.songTime + Game.HIT_MARGIN) {
			var noteInfo = chart[realChartIndex];

			// Add notes at chords
			if (expectedHits.TryGetValue(noteInfo.time, out var list)) {
				list.Add(noteInfo);
			} else {
				var l = new List<NoteInfo>() { noteInfo };
				expectedHits.Add(noteInfo.time, l);
			}
			realChartIndex++;
		}

		// Update real input
		foreach (var kv in expectedHits.ToArray()) {
			var chord = kv.Value;

			// Handle misses
			if (Game.Instance.songTime - chord[0].time > Game.HIT_MARGIN) {
				expectedHits.Remove(chord[0].time);
			}

			// Handle hits
			if (Game.Instance.StrumThisFrame) {
				// Convert NoteInfo list to chord fret array
				int[] chordInts = new int[chord.Count];
				for (int i = 0; i < chordInts.Length; i++) {
					chordInts[i] = chord[i].fret;
				}

				// Check if correct chord is pressed
				if (ChordPressed(chordInts)) {
					// If so, hit!
					expectedHits.Remove(chord[0].time);

					foreach (var hit in chord) {
						// Destroy notes
						if (spawnedNotes.TryGetValue(hit, out Note note)) {
							Destroy(note.gameObject);
							spawnedNotes.Remove(hit);
						}

						// Spawn particles
						var p = Instantiate(hitParticles, frets[hit.fret].transform);
						p.transform.localPosition = Vector3.zero;
						p.transform.localRotation = Quaternion.identity;
						p.GetComponent<Colorizer>().color = fretColors[hit.fret];
					}
				} else {
					// Else we missed...
					expectedHits.Remove(chord[0].time);
				}
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
	}

	private void SpawnNote(NoteInfo noteInfo) {
		var noteObj = Instantiate(note, transform);
		noteObj.transform.localPosition = new Vector3(fretPositions[noteInfo.fret], 0f, 2f);

		var noteComp = noteObj.GetComponent<Note>();
		noteComp.SetColor(fretColors[noteInfo.fret]);

		spawnedNotes.Add(noteInfo, noteComp);
	}
}