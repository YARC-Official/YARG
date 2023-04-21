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

		private FiveFretInputStrategy input;

		[Space]
		[SerializeField]
		private Fret[] frets;
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
		private bool[] extendedSustain = new bool[] {false,false,false,false,false};

		// https://www.reddit.com/r/Rockband/comments/51t3c0/exactly_how_many_points_are_sustains_worth/
		private const double SUSTAIN_PTS_PER_BEAT = 12.0;
		private const int PTS_PER_NOTE = 25;

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
				fret.SetColor(commonTrack.FretColor(i), commonTrack.SustainColor(i));
				frets[i] = fret;
			}
			openNoteParticles.Colorize(commonTrack.FretColor(5));

			// initialize scoring variables
			starsKeeper = new(Chart, scoreKeeper,
				player.chosenInstrument,
				PTS_PER_NOTE);
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			input.FretChangeEvent -= FretChangedAction;
			input.StrumEvent -= StrumAction;
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
				} else if (eventInfo.name == $"solo_{player.chosenInstrument}") {
					SoloSection = eventInfo;
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
			while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= Play.Instance.SongTime + Constants.HIT_MARGIN) {
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

				// Sustain scoring
				scoreKeeper.Add(susTracker.Update(heldNote) * Multiplier * SUSTAIN_PTS_PER_BEAT);

				if (heldNote.time + heldNote.length <= Play.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					susTracker.Drop(heldNote);
					frets[heldNote.fret].StopAnimation();
					frets[heldNote.fret].StopSustainParticles();

					extendedSustain[heldNote.fret] = false;
				}
			}

			UpdateInput();

			// Un-strum
			strummed = false;
		}

		public override void SetReverb(bool on) {
			switch (player.chosenInstrument) {
				case "guitar":
					Play.Instance.ReverbAudio("guitar", on);
					break;
				case "rhythm":
					Play.Instance.ReverbAudio("rhythm", on);
					break;
				case "bass":
					Play.Instance.ReverbAudio("bass", on);
					break;
				case "keys":
					Play.Instance.ReverbAudio("keys", on);
					break;
			}

			Play.Instance.ReverbAudio("song", on);
		}

		private void UpdateInput() {
			// Only want to decrease strum leniency on frames where we didn't strum
			bool strummedCurrentNote = false;
			if (strumLeniency > 0f && !strummed) {
				strumLeniency -= Time.deltaTime;

				if (strumLeniency <= 0f) {
					UpdateOverstrums();
					strumLeniency = 0f;
				} else {
					RemoveOldAllowedOverstrums();
					if (IsOverstrumForgiven()) { // Consume allowed overstrum as soon as it's "hit"
						strummedCurrentNote = true;
						strumLeniency = 0f;
					}
				}
			}


			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Constants.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					hitChartIndex++;
					missedAnyNote = true;
					notePool.MissNote(hit);
					StopAudio = true;
					extendedSustain[hit.fret] = false;
				}
				allowedOverstrums.Clear(); // Disallow all overstrums upon missing
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

			// If the note is a HOPO, the player has not strummed, and the HOPO can't be hit, nothing happens.
			if (chord[0].hopo && !strummed && strumLeniency == 0f) {
				if (Combo <= 0) {
					return;
				}

				// If infinite front-end window is disabled and the latest input is outside of the timing window, nothing happened.
				if (!Constants.INFINITE_FRONTEND && latestInput.HasValue &&
					Play.Instance.SongTime - latestInput.Value > Constants.HIT_MARGIN) {

					return;
				}
			}

			// If strumming to recover combo, skip to first valid note within the timing window.
			// This will make it easier to recover.
			if ((strummed || strumLeniency > 0f) && !ChordPressed(chord)) {
				RemoveOldAllowedOverstrums();
				var overstrumForgiven = IsOverstrumForgiven(false); // Do NOT consume allowed overstrums; this is done in other parts of the code

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
				// Overstrums are dealt with at the top of the method
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
			strummedCurrentNote = strummedCurrentNote || strummed || strumLeniency > 0f;
			strumLeniency = 0f;
			foreach (var hit in chord) {
				hitChartIndex++;
				// Hit notes
				notePool.HitNote(hit);
				StopAudio = false;

				// Play particles and animation
				if (hit.fret != 5) {
					frets[hit.fret].PlayParticles();
					frets[hit.fret].PlayAnimation();
				} else {
					openNoteParticles.Play();
				}

				// If sustained, add to held
				if (hit.length > 0.2f) {
					heldNotes.Add(hit);
					frets[hit.fret].PlaySustainParticles();
					scoreKeeper.Add(susTracker.Strum(hit) * Multiplier * SUSTAIN_PTS_PER_BEAT);
          
					frets[hit.fret].PlayAnimationSustainsLooped();

					// Check if it's extended sustain;
					var nextNote = GetNextNote(hit.time);
					if (nextNote != null) {
						extendedSustain[hit.fret] = hit.EndTime > nextNote.time;
					}
				} else if (hit.fret != 5) {
					extendedSustain[hit.fret] = false;
				}

				// Add stats
				notesHit++;
				scoreKeeper.Add(PTS_PER_NOTE * Multiplier);

				// Solo stuff
				if (Play.Instance.SongTime >= SoloSection?.time && Play.Instance.SongTime <= SoloSection?.EndTime) {
					soloNotesHit++;
				} else if (Play.Instance.SongTime >= SoloSection?.EndTime + 10) {
					soloNotesHit = 0;
				}
			}

			// If this is a tap note, and it was hit without strumming,
			// add it to the allowed overstrums. This is so the player
			// doesn't lose their combo when they strum AFTER they hit
			// the tap note.
			if (chord[0].hopo && !strummedCurrentNote) {
				allowedOverstrums.Clear(); // Only allow overstrumming latest HO/PO
				allowedOverstrums.Add(chord);
			} else if (allowedOverstrums.Count > 0 && !chord[0].hopo) {
				for (int i = 0; i < allowedOverstrums.Count; i++) {
					if (!ChordEquals(chord, allowedOverstrums[i])) {
						allowedOverstrums.Clear(); // If latest strum is different from latest HO/PO, disallow overstrumming
						break;
					} else {
						// Refresh time (for long same-fret strum sequences)
						foreach (var hit in allowedOverstrums[i]) {
							hit.time = chord[0].time;
						}
					}
				}

			}
		}

		private void RemoveOldAllowedOverstrums() {
			// Remove all old allowed overstrums
			while (allowedOverstrums.Count > 0
				&& Play.Instance.SongTime - allowedOverstrums[0][0].time > Constants.HIT_MARGIN) {

				allowedOverstrums.RemoveAt(0);
			}
		}

		private bool IsOverstrumForgiven(bool remove = true) {
			for (int i = 0; i < allowedOverstrums.Count; i++) {
				if (ChordPressed(allowedOverstrums[i], true)) {
					// If we found a chord that was pressed, remove 
					// all of the allowed overstrums before it.
					// This prevents over-forgiving overstrums.

					if (remove) {
						for (int j = i; j >= 0; j--) {
							allowedOverstrums.RemoveAt(j);
						}
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
				susTracker.Drop(heldNote);
				frets[heldNote.fret].StopAnimation();
				frets[heldNote.fret].StopSustainParticles();
				extendedSustain[heldNote.fret] = false;
			}
		}

		private bool ChordPressed(List<NoteInfo> chordList, bool overstrumCheck = false) {
			// Convert NoteInfo list to chord fret array
			bool overlap = ChordsOverlap(heldNotes, chordList);
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
						if (overlap && heldNotes.Any(j => j.fret == i)) {
							continue;
						}

						if (frets[i].IsPressed && i > fret) {
							return false;
						} else if (!frets[i].IsPressed && i == fret) {
							return false;
						} else if (frets[i].IsPressed && i != fret && !Constants.ANCHORING) {
							return false;
						}
					}
				}
			} else {
				// Deal with multi-key chords
				for (int i = 0; i < frets.Length; i++) {
					// Skip any notes that are currently held down.
					// Extended sustains.
					if (overlap && heldNotes.Any(j => j.fret == i)) {
						continue;
					}

					bool contains = chord.Contains(i);
					if (contains && !frets[i].IsPressed) {
						return false;
					} else if (!contains && frets[i].IsPressed) {
						if (Constants.ANCHORING && Constants.ANCHOR_CHORD_HOPO &&
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

			if (pressed) {
				// Let go of held notes if wrong note pressed
				if (!IsExtendedSustain()) { // Unless it's extended sustains
					bool release = false;
					//
					for (int i = heldNotes.Count - 1; i >= 0; i--) {
						var heldNote = heldNotes[i];
						if (heldNote.fret == fret || (heldNotes.Count == 1 && fret < heldNote.fret)) { // Button press is valid
							continue;
						} else { // Wrong button pressed; release all sustains
							release = true;
							break;
						}
					}
					if (release) { // Actually release all sustains
						for (int i = heldNotes.Count - 1; i >= 0; i--) {
							var heldNote = heldNotes[i];
							notePool.MissNote(heldNote);
							heldNotes.RemoveAt(i);
							frets[heldNote.fret].StopAnimation();
							frets[heldNote.fret].StopSustainParticles();
							extendedSustain[heldNote.fret] = false;
							StopAudio = true;
						}
					}
				}
			} else {
				// Let go of held notes
				NoteInfo letGo = null;
				for (int i = heldNotes.Count - 1; i >= 0; i--) {
					var heldNote = heldNotes[i];
					if (IsExtendedSustain() && heldNote.fret != fret || (heldNotes.Count == 1 && fret < heldNote.fret)) {
						continue;
					}

					notePool.MissNote(heldNote);
					heldNotes.RemoveAt(i);
					susTracker.Drop(heldNote);
					frets[heldNote.fret].StopAnimation();
					frets[heldNote.fret].StopSustainParticles();
					extendedSustain[heldNote.fret] = false;

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
			if (!input.botMode) {
				strumLeniency = Constants.STRUM_LENIENCY;
			}
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
			startFCDetection = true;
			noteComp.SetInfo(
				commonTrack.NoteColor(noteInfo.fret),
				commonTrack.SustainColor(noteInfo.fret),
				noteInfo.length,
				model
			);
		}

		private string PrintFrets() { // Debug function; remove later?
			return "[" + (frets[0].IsPressed ? "G" : "") + (frets[1].IsPressed ? "R" : "") + (frets[2].IsPressed ? "Y" : "") + (frets[3].IsPressed ? "B" : "") + (frets[4].IsPressed ? "O" : "") + "]";
		}

		private bool ChordEquals(List<NoteInfo> chordList1, List<NoteInfo> chordList2) {
			int[] chord1 = new int[chordList1.Count];
			for (int i = 0; i < chord1.Length; i++) {
				chord1[i] = chordList1[i].fret;
			}
			int[] chord2 = new int[chordList2.Count];
			for (int i = 0; i < chord2.Length; i++) {
				chord2[i] = chordList2[i].fret;
			}
			for (int i = 0; i < 5; i++) {
				if ((chord1.Contains(i) && !chord2.Contains(i)) || (!chord1.Contains(i) && chord2.Contains(i))) {
					return false;
				}
			}
			return true;
		}

		private bool ChordsOverlap(List<NoteInfo> chordList1, List<NoteInfo> chordList2) {
			foreach (NoteInfo chord in chordList1) {
				if (chord.length > 0.2f && chord.EndTime > chordList2[0].time) { // If it's a sustain and overlaps with next note...
					return true;
				}
			}
			return false;
		}

		private NoteInfo GetNextNote(float currentChordTime) {
			var i = hitChartIndex;
			while (Chart.Count > i) {
				if (Chart[i].time > currentChordTime) {
					return Chart[i];
				}
				i++;
			}
			return null;
		}

		private bool IsExtendedSustain() {
			return extendedSustain.Any(x => x);
		}
	}
}