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
	private NotePool notePool;

	private Fret[] frets = null;
	private int visualChartIndex = 0;
	private int realChartIndex = 0;

	private SortedDictionary<float, List<NoteInfo>> expectedHits = new();

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
		var uvs = meshFilter.mesh.uv;
		for (int i = 0; i < uvs.Length; i++) {
			uvs[i] += new Vector2(0f, Time.deltaTime * Game.Instance.SongSpeed);
		}
		meshFilter.mesh.uv = uvs;

		// Update visuals
		float relativeTime = Game.Instance.SongTime + (3.75f / Game.Instance.SongSpeed);
		var chart = Game.Instance.Chart;

		// Since chart is sorted, this is guaranteed to work
		while (chart.Count > visualChartIndex && chart[visualChartIndex].time <= relativeTime) {
			var noteInfo = chart[visualChartIndex];

			SpawnNote(noteInfo, relativeTime);
			visualChartIndex++;
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
					// Destroy notes
					notePool.RemoveNote(hit);

					// Play particles
					frets[hit.fret].PlayParticles();
				}

				// Only hit one note per frame
				break;
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

	private void SpawnNote(NoteInfo noteInfo, float time) {
		float lagCompensation = (time - noteInfo.time) * Game.Instance.SongSpeed;
		var pos = new Vector3(fretPositions[noteInfo.fret], 0f, 2f - lagCompensation);

		var noteComp = notePool.CreateNote(noteInfo, pos);
		noteComp.SetColor(fretColors[noteInfo.fret]);
	}
}