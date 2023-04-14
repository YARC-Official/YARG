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
		private float strumLeniency;
<<<<<<< HEAD
		
=======

>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec
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
		[SerializeField]
		private ParticleGroup openNoteParticles;

		private Queue<List<NoteInfo>> expectedHits = new();
		private List<List<NoteInfo>> allowedOverstrums = new();
		private List<NoteInfo> heldNotes = new();
		private float? latestInput = null;
		private bool latestInputIsStrum = false;

		private int notesHit = 0;

		protected override void StartTrack() {
			notePool.player = player;
			genericPool.player = player;

			// Lefty flip

			if (player.leftyFlip) {
				frets = frets.Reverse().ToArray();
			}

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
			openNoteParticles.Colorize(fretColors[5]);
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			input.FretChangeEvent -= FretChangedAction;
			input.StrumEvent -= StrumAction;

			// Set score
			player.lastScore = new PlayerManager.LastScore {
				percentage = new DiffPercent {
					difficulty = player.chosenDifficulty,
					percent = Chart.Count == 0 ? 1f : (float) notesHit / Chart.Count
				},
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

			// Update events (beat lines, starpower, etc.)
			var events = Play.Instance.chart.events;
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
			while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= Play.Instance.SongTime + Play.HIT_MARGIN) {
				var noteInfo = Chart[inputChartIndex];

				var peeked = expectedHits.ReversePeekOrNull();
				if (peeked?[0].time == noteInfo.time) {
					// Add notes as chords
					peeked.Add(noteInfo);
				} else {
					// Or add notes as singular
					var l = new List<NoteInfo>(5) { noteInfo };
					expectedHits.Enqueue(l);
				}

				inputChartIndex++;
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
			// Only want to decrease strum leniency on frames where we didn't strum
			if (strumLeniency > 0f && !strummed) {
				strumLeniency -= Time.deltaTime;
<<<<<<< HEAD
				
=======

>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec
				if (strumLeniency <= 0f) {
					UpdateOverstrums();
				}
			}
<<<<<<< HEAD
			
=======

>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec
			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Play.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					hitChartIndex++;
					notePool.MissNote(hit);
					StopAudio = true;
				}
			}

			if (expectedHits.Count <= 0) {
				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();

			// If the note is not a HOPO and the player has not strummed, nothing happens.
			if (!chord[0].hopo && !strummed && strumLeniency == 0f) {
				return;
			}

<<<<<<< HEAD
			// If the note is a HOPOm the player has not strummed, and the HOPO can't be hit, nothing happens.
=======
			// If the note is a HOPO, the player has not strummed, and the HOPO can't be hit, nothing happens.
>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec
			if (chord[0].hopo && !strummed && strumLeniency == 0f) {
				if (Combo <= 0) {
					return;
				}

				// If infinite front-end window is disabled and the latest input is outside of the timing window, nothing happened.
				if (!Play.INFINITE_FRONTEND && latestInput.HasValue && Play.Instance.SongTime - latestInput.Value > Play.HIT_MARGIN) {
					return;
				}
			}

			// If strumming to recover combo, skip to first valid note within the timing window.
			// This will make it easier to recover.
			if ((strummed || strumLeniency > 0f) && !ChordPressed(chord)) {
				RemoveOldAllowedOverstrums();
				var overstrumForgiven = IsOverstrumForgiven();

				// Ensure allowed overstrums won't break combos
				if (!overstrumForgiven) {
					// Look for the chord that is trying to be hit
					var found = false;
					foreach (var newChord in expectedHits) {
						// Stop looking if a valid note to strum was found
						if (ChordPressed(newChord)) {
							found = true;
							chord = newChord;
							break;
						}
					}

					// If found...
					if (found) {
						// Miss all notes previous to the strummed note
						while (expectedHits.Peek() != chord) {
							var missedChord = expectedHits.Dequeue();
							foreach (var hit in missedChord) {
								hitChartIndex++;
								notePool.MissNote(hit);
							}
						}

						// Reset the combo (it will be added to later on)
						Combo = 0;
					}
				}
			}

			// Check if correct chord is pressed
			if (!ChordPressed(chord)) {
<<<<<<< HEAD
				// if (!chord[0].hopo && strumLeniency == 0f) {
				// 	UpdateOverstrums();
				// }
=======
				// Overstrums are dealt with at the top of the method
>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec

				return;
			}

			// Avoid multi-hits
			if (chord[0].hopo) {
				// If latest input is cleared, it was already used
				if (latestInput == null) {
					return;
				}

				// Allow 1 multi-hit if latest note was strummed (for charts like Zoidberg the Cowboy by schmutz06)
				if (latestInputIsStrum) {
					latestInputIsStrum = false;
				} else {
					// Input is valid; clear it to avoid multi-hit later
					latestInput = null;
				}
			}

			// If correct chord is pressed, and is not a multi-hit, hit it!
			expectedHits.Dequeue();

			Combo++;
			strumLeniency = 0f;
			foreach (var hit in chord) {
				hitChartIndex++;
				// Hit notes
				notePool.HitNote(hit);
				StopAudio = false;

				// Play particles
				if (hit.fret != 5) {
					frets[hit.fret].PlayParticles();
				} else {
					openNoteParticles.Play();
				}

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

		private void RemoveOldAllowedOverstrums() {
			// Remove all old allowed overstrums
			while (allowedOverstrums.Count > 0
				&& Play.Instance.SongTime - allowedOverstrums[0][0].time > Play.HIT_MARGIN) {

				allowedOverstrums.RemoveAt(0);
			}
		}

		private bool IsOverstrumForgiven() {
			for (int i = 0; i < allowedOverstrums.Count; i++) {
				if (ChordPressed(allowedOverstrums[i], true)) {
					// If we found a chord that was pressed, remove 
					// all of the allowed overstrums before it.
					// This prevents over-forgiving overstrums.

					for (int j = i; j >= 0; j--) {
						allowedOverstrums.RemoveAt(j);
					}

					// Overstrum forgiven!
					return true;
				}
			}

			return false;
		}

		private void UpdateOverstrums() {
			RemoveOldAllowedOverstrums();

			// Look in the allowed overstrums first
			if (IsOverstrumForgiven()) {
				return;
			}
			
			Combo = 0;
			strumLeniency = 0f;

			// Let go of held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];

				notePool.MissNote(heldNote);
				StopAudio = true;

				heldNotes.RemoveAt(i);
				frets[heldNote.fret].StopSustainParticles();
			}
		}

		private bool ChordPressed(List<NoteInfo> chordList, bool overstrumCheck = false) {
			// Convert NoteInfo list to chord fret array
			int[] chord = new int[chordList.Count];
			for (int i = 0; i < chord.Length; i++) {
				chord[i] = chordList[i].fret;
			}

			if (chord.Length == 1) {
				int fret = chord[0];

				if (fret == 5) {
					// Deal with open notes
					for (int i = 0; i < frets.Length; i++) {
						if (frets[i].IsPressed) {
							return false;
						}
					}
				} else {
					// Deal with single notes
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
						if (Play.ANCHORING && Play.ANCHOR_CHORD_HOPO &&
							chordList[0].hopo && !(strummed || strumLeniency > 0f || overstrumCheck) &&
							i < chordList[0].fret) {

							// Allow anchoring chord HO/POs
							continue;
						} else {
							return false;
						}
					}
				}
			}

			return true;
		}

		private void FretChangedAction(bool pressed, int fret) {
			latestInput = Play.Instance.SongTime;
			latestInputIsStrum = false;

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
			latestInput = Play.Instance.SongTime;
			latestInputIsStrum = true;

			// Strum leniency already active and another strum inputted, a double strum occurred (must overstrum)
			if (strumLeniency > 0f) {
				UpdateOverstrums();
			}

			strummed = true;
<<<<<<< HEAD

			// Strum leniency already active and another strum inputted, a double strum occurred (must overstrum)
			if (strumLeniency > 0f) {
				UpdateOverstrums();
			}
			
=======
>>>>>>> 38e8565f23092f84eafa19debdb4a25f05582dec
			strumLeniency = Play.STRUM_LENIENCY;
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			float x = noteInfo.fret == 5 ? 0f : frets[noteInfo.fret].transform.localPosition.x;
			var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			// Get model type
			var model = NoteComponent.ModelType.NOTE;
			if (noteInfo.fret == 5) {
				model = NoteComponent.ModelType.FULL;
			} else if (noteInfo.hopo) {
				model = NoteComponent.ModelType.HOPO;
			}

			// Set note info
			var noteComp = notePool.AddNote(noteInfo, pos);
			noteComp.SetInfo(fretColors[noteInfo.fret], noteInfo.length, model);
		}
	}
}