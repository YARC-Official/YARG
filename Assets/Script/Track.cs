using System;
using System.Collections.Generic;
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

	private Fret[] frets = null;
	private int visualChartIndex = 0;
	private int realChartIndex = 0;

	private Dictionary<NoteInfo, Note> spawnedNotes = new();
	private List<NoteInfo> expectedHits = new();

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

			expectedHits.Add(noteInfo);
			realChartIndex++;
		}

		// Update real input
		for (int i = expectedHits.Count - 1; i >= 0; i--) {
			var hit = expectedHits[i];

			// Handle misses
			if (Game.Instance.songTime - hit.time > Game.HIT_MARGIN) {
				expectedHits.RemoveAt(i);
				Debug.Log("missed: " + hit.fret);
			}

			// Handle hits
			if (frets[hit.fret].IsPressed && Game.Instance.StrumThisFrame) {
				expectedHits.RemoveAt(i);

				if (spawnedNotes.TryGetValue(hit, out Note note)) {
					Destroy(note.gameObject);
					spawnedNotes.Remove(hit);
				}
			}
		}
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