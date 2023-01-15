using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Util;

namespace YARG.PlayMode {
	public class FiveFretTrack : AbstractTrack {
		private bool strummed = false;
		private FiveFretInputStrategy input;

		[Space]
		[SerializeField]
		private Fret[] frets;
		[SerializeField]
		private Color[] fretColors;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;

		private List<NoteInfo> Chart => Play.Instance.chart
			.GetChartByName(player.chosenInstrument)[player.chosenDifficulty];

		private int visualChartIndex = 0;
		private int realChartIndex = 0;
		private int eventChartIndex = 0;

		private Queue<List<NoteInfo>> expectedHits = new();
		private List<List<NoteInfo>> allowedOverstrums = new();
		private List<NoteInfo> heldNotes = new();

		private int notesHit = 0;

		protected override void StartTrack() {
			notePool.player = player;
			genericPool.player = player;

			// Inputs

			input = (FiveFretInputStrategy) player.inputStrategy;
			input.ResetForSong();

			input.FretChangeEvent += FretChangedAction;
			input.StrumEvent += StrumAction;

			// Color frets
			for (int i = 0; i < 5; i++) {
				var fret = frets[i].GetComponent<Fret>();
				fret.SetColor(fretColors[i]);
				frets[i] = fret;
			}
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			input.FretChangeEvent -= FretChangedAction;
			input.StrumEvent -= StrumAction;

			// Set score
			player.lastScore = new PlayerManager.Score {
				percentage = (float) notesHit / Chart.Count,
				notesHit = notesHit,
				notesMissed = Chart.Count - notesHit
			};
		}

		protected override void UpdateTrack() {
			// Update input strategy
			if (input.botMode) {
				input.UpdateBotMode(Chart, Play.Instance.SongTime);
			} else {
				input.UpdatePlayerMode();
			}

			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			var events = Play.Instance.chart.events;

			// Update events (beat lines, starpower, etc.)
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= RelativeTime) {
				var eventInfo = events[eventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				if (eventInfo.name == "beatLine_minor") {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				} else if (eventInfo.name == "beatLine_major") {
					genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				} else if (eventInfo.name == $"starpower_{player.chosenInstrument}") {
					StarpowerSection = eventInfo;
				}

				eventChartIndex++;
			}

			// Since chart is sorted, this is guaranteed to work
			while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= RelativeTime) {
				var noteInfo = Chart[visualChartIndex];

				SpawnNote(noteInfo, RelativeTime);
				visualChartIndex++;
			}

			// Update expected input
			while (Chart.Count > realChartIndex && Chart[realChartIndex].time <= Play.Instance.SongTime + Play.HIT_MARGIN) {
				var noteInfo = Chart[realChartIndex];

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

			// Update held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];
				if (heldNote.time + heldNote.length <= Play.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}

			UpdateInput();

			// Un-strum
			strummed = false;
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Play.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					notePool.MissNote(hit);
					StopAudio = true;
				}
			}

			if (expectedHits.Count <= 0) {
				UpdateOverstrums();
				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();
			if (!chord[0].hopo && !strummed) {
				return;
			} else if (chord[0].hopo && Combo <= 0 && !strummed) {
				return;
			}

			// Check if correct chord is pressed
			if (!ChordPressed(chord)) {
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
				StopAudio = false;

				// Play particles
				frets[hit.fret].PlayParticles();

				// If sustained, add to held
				if (hit.length > 0.2f) {
					heldNotes.Add(hit);
					frets[hit.fret].PlaySustainParticles();
				}

				// Add stats
				notesHit++;
			}

			// If this is a tap note, and it was hit without strumming,
			// add it to the allowed overstrums. This is so the player
			// doesn't lose their combo when they strum AFTER they hit
			// the tap note.
			if (chord[0].hopo && !strummed) {
				allowedOverstrums.Add(chord);
			} else if (!chord[0].hopo) {
				allowedOverstrums.Clear();
			}
		}

		private void UpdateOverstrums() {
			// Remove all old allowed overstrums
			while (allowedOverstrums.Count > 0
				&& Play.Instance.SongTime - allowedOverstrums[0][0].time > Play.HIT_MARGIN) {

				allowedOverstrums.RemoveAt(0);
			}

			// Don't do anything else if we didn't strum
			if (!strummed) {
				return;
			}

			// Look in the allowed overstrums first
			for (int i = 0; i < allowedOverstrums.Count; i++) {
				if (ChordPressed(allowedOverstrums[i])) {
					// If we found a chord that was pressed, remove 
					// all of the allowed overstrums before it.
					// This prevents over-forgiving overstrums.

					for (int j = i; j >= 0; j--) {
						allowedOverstrums.RemoveAt(j);
					}

					// Overstrum forgiven!
					return;
				}
			}

			Combo = 0;

			// Let go of held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];

				notePool.MissNote(heldNote);
				StopAudio = true;

				heldNotes.RemoveAt(i);
				frets[heldNote.fret].StopSustainParticles();
			}
		}

		private bool ChordPressed(List<NoteInfo> chordList) {
			// Convert NoteInfo list to chord fret array
			int[] chord = new int[chordList.Count];
			for (int i = 0; i < chord.Length; i++) {
				chord[i] = chordList[i].fret;
			}

			if (chord.Length == 1) {
				// Deal with single notes
				int fret = chord[0];
				for (int i = 0; i < frets.Length; i++) {
					// Skip any notes that are currently held down.
					// Extended sustains.
					if (heldNotes.Any(j => j.fret == i)) {
						continue;
					}

					if (frets[i].IsPressed && i > fret) {
						return false;
					} else if (!frets[i].IsPressed && i == fret) {
						return false;
					} else if (frets[i].IsPressed && i != fret && !Play.ANCHORING) {
						return false;
					}
				}
			} else {
				// Deal with multi-key chords
				for (int i = 0; i < frets.Length; i++) {
					// Skip any notes that are currently held down.
					// Extended sustains.
					if (heldNotes.Any(j => j.fret == i)) {
						continue;
					}

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

		private void FretChangedAction(bool pressed, int fret) {
			frets[fret].SetPressed(pressed);

			if (!pressed) {
				// Let go of held notes
				NoteInfo letGo = null;
				for (int i = heldNotes.Count - 1; i >= 0; i--) {
					var heldNote = heldNotes[i];
					if (heldNote.fret != fret) {
						continue;
					}

					notePool.MissNote(heldNote);
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();

					letGo = heldNote;
				}

				// Only stop audio if all notes were let go and...
				if (letGo != null && heldNotes.Count <= 0) {
					// ...if the player let go of the note more than 0.15s
					// before the end of the note. This prevents the game
					// from stopping the audio if the player let go a handful
					// of milliseconds too early (which is okay).
					float endTime = letGo.time + letGo.length;
					if (endTime - Play.Instance.SongTime > 0.15f) {
						StopAudio = true;
					}
				}
			}
		}

		private void StrumAction() {
			strummed = true;
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			float x = frets[noteInfo.fret].transform.localPosition.x;
			var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			// Set note info
			var noteComp = notePool.AddNote(noteInfo, pos);
			noteComp.SetInfo(fretColors[noteInfo.fret], noteInfo.length, noteInfo.hopo);
		}
	}
}