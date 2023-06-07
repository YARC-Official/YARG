using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Settings;
using YARG.Util;

namespace YARG.PlayMode {
	public class FiveFretTrack : AbstractTrack {
		// I CAN'T WAIT UNTIL THE NEW ENGINE!!!!

		private bool strummed = false;
		private float strumLeniency;

		private FiveFretInputStrategy input;

		[Space]
		[SerializeField]
		private Fret[] frets;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private ParticleGroup openNoteParticles;

		private Queue<List<NoteInfo>> expectedHits = new();
		private List<List<NoteInfo>> allowedOverstrums = new();
		private List<NoteInfo> heldNotes = new();
		private List<NoteInfo> lastHitNote = null;
		private float? latestInput = null;
		private bool latestInputIsStrum = false;
		private bool[] extendedSustain = new bool[] { false, false, false, false, false, false };
		private int allowedGhostsDefault = Constants.EXTRA_ALLOWED_GHOSTS + 1;
		private int allowedGhosts = Constants.EXTRA_ALLOWED_GHOSTS + 1;
		private int[] allowedChordGhosts = new int[] { -1, -1, -1, -1, -1 }; // -1 = not a chord; 0 = ghosted; 1 = ghost allowed

		// https://www.reddit.com/r/Rockband/comments/51t3c0/exactly_how_many_points_are_sustains_worth/
		private const double SUSTAIN_PTS_PER_BEAT = 25.0;
		private const int PTS_PER_NOTE = 50;
		private int noteCount = -1;

		private float whammyAmount;
		private bool whammyLastNote;
		private float whammyAnimationAmount;

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
			input.WhammyEvent += WhammyEvent;

			if (input.BotMode) {
				input.InitializeBotMode(Chart);
			}

			// Color frets
			for (int i = 0; i < 5; i++) {
				var fret = frets[i].GetComponent<Fret>();
				fret.SetColor(commonTrack.FretColor(i), commonTrack.FretInnerColor(i), commonTrack.SustainColor(i));
				frets[i] = fret;
			}
			openNoteParticles.Colorize(commonTrack.FretColor(5));

			// initialize scoring variables
			starsKeeper = new(Chart, scoreKeeper,
				player.chosenInstrument,
				PTS_PER_NOTE);

			noteCount = GetChartCount();
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			input.FretChangeEvent -= FretChangedAction;
			input.StrumEvent -= StrumAction;
			input.WhammyEvent -= WhammyEvent;
		}

		protected override void UpdateTrack() {
			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			// Since chart is sorted, this is guaranteed to work
			while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= TrackStartTime) {
				var noteInfo = Chart[visualChartIndex];

				SpawnNote(noteInfo, TrackStartTime);
				visualChartIndex++;
			}

			// Update expected input
			while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= HitMarginStartTime) {
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

				if (heldNote.time + heldNote.length <= CurrentTime) {
					heldNotes.RemoveAt(i);
					susTracker.Drop(heldNote);
					if (heldNote.fret < 5) frets[heldNote.fret].StopAnimation(); // TEMP (remove check later)
					if (heldNote.fret < 5) frets[heldNote.fret].StopSustainParticles(); // TEMP (remove check later)

					extendedSustain[heldNote.fret] = false;

					if (heldNotes.Count == 0) {
						whammyLastNote = false;
					}
				}
			}

			UpdateInput();

			// Un-strum
			strummed = false;
		}

		protected override void UpdateStarpower() {
			if (IsStarpowerHit() && heldNotes.Count != 0) {
				whammyLastNote = true;
			}

			base.UpdateStarpower();

			// Update whammy amount and animation
			if (whammyAmount > 0f) {
				whammyAmount -= Time.deltaTime;
				whammyAnimationAmount = Mathf.Lerp(whammyAnimationAmount, 1f, Time.deltaTime * 6f);
			} else {
				whammyAnimationAmount = Mathf.Lerp(whammyAnimationAmount, 0f, Time.deltaTime * 3f);
			}
			notePool.WhammyFactor = whammyAnimationAmount;

			// Add starpower on whammy, only if there are held notes
			if ((heldNotes.Count == 0 || CurrentStarpower?.time > CurrentTime || CurrentStarpower == null) && !whammyLastNote) {
				return;
			}

			// Update starpower
			if (whammyAmount > 0f) {
				starpowerCharge += Time.deltaTime * Play.Instance.CurrentBeatsPerSecond * 0.034f;
			}
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
		}

		private void UpdateInput() {
			// Only want to decrease strum leniency on frames where we didn't strum
			bool strummedCurrentNote = false;
			bool strumLeniencyEnded = false;
			if (strumLeniency > 0f && !strummed) {
				strumLeniency -= Time.deltaTime;

				if (strumLeniency <= 0f) {
					//UpdateOverstrums();
					strumLeniency = 0f;
					strumLeniencyEnded = true;
				} else {
					RemoveOldAllowedOverstrums();
					if (IsOverstrumForgiven()) { // Consume allowed overstrum as soon as it's "hit"
						strummedCurrentNote = true;
						strumLeniency = 0f;
					}
				}
			}


			// Handle misses (multiple a frame in case of lag)
			while (HitMarginEndTime > expectedHits.PeekOrNull()?[0].time) {
				var missedChord = expectedHits.Dequeue();
				ResetAllowedChordGhosts();
				// Call miss for each component
				Combo = 0;
				missedAnyNote = true;
				StopAudio = true;
				lastHitNote = null;
				foreach (var hit in missedChord) {
					hitChartIndex++;
					notePool.MissNote(hit);
					if (hit.fret < 5) extendedSustain[hit.fret] = false;
				}
				allowedOverstrums.Clear(); // Disallow all overstrums upon missing
			}

			if (expectedHits.Count <= 0) {
				if (strumLeniencyEnded) {
					UpdateOverstrums();
				}
				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();

			// If the note is not a HOPO or tap and the player has not strummed, nothing happens.
			if (!chord[0].hopo && !chord[0].tap && !strummed && strumLeniency == 0f) {
				return;
			}

			bool returnLater = false;
			// If the note is a HOPO, the player has not strummed, and the HOPO can't be hit, nothing happens.
			if ((chord[0].hopo || chord[0].tap) && !strummed && strumLeniency == 0f) {
				if (Combo <= 0 && chord[0].hopo) {
					return;
				} else if (allowedGhosts <= 0) {
					returnLater = true;
				}

				// If infinite front-end window is disabled and the latest input is outside of the timing window, nothing happened.
				if (!Constants.INFINITE_FRONTEND && latestInput.HasValue && HitMarginEndTime > latestInput.Value) {
					return;
				}
			}

			// If strumming to recover combo, skip to first valid note within the timing window.
			// This will make it easier to recover.
			bool recoveryStrum = Combo > 0 ? strumLeniencyEnded : (strummed || strumLeniency > 0f);
			if (recoveryStrum && !ChordPressed(chord)) {
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
						strumLeniencyEnded = false;
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
						missedAnyNote = true;
					}
				}
			}
			if (strumLeniencyEnded) { // Strum leniency ended, and suitable strummable note wasn't found; overstrum
				UpdateOverstrums();
			}
			// If tapping to recover combo during tap note section, skip to first valid note within the timing window.
			// This will make it easier to recover.
			if (Constants.EASY_TAP_RECOVERY && Combo <= 0 && chord[0].tap && !ChordPressed(chord)) {
				var found = false;
				foreach (var newChord in expectedHits) {
					if (!newChord[0].tap) {
						break;
					}
					// Stop looking if a valid note to tap was found
					if (ChordPressed(newChord) && newChord[0].fret != 5) {
						found = true;
						returnLater = false;
						chord = newChord;
						break;
					}
				}

				// If found...
				if (found) {
					// Miss all notes previous to the tapped note
					while (expectedHits.Peek() != chord) {
						var missedChord = expectedHits.Dequeue();
						foreach (var hit in missedChord) {
							hitChartIndex++;
							notePool.MissNote(hit);
						}
					}

					// Reset the combo (it will be added to later on)
					Combo = 0;
					missedAnyNote = true;
				}
			}

			// Check if correct chord is pressed
			if (returnLater || !ChordPressed(chord)) {
				// Overstrums are dealt with at the top of the method
				return;
			}

			// Avoid multi-hits
			if (chord[0].hopo || chord[0].tap) {
				// If latest input is cleared, it was already used
				if (latestInput == null) {
					return;
				}

				// Allow 1 multi-hit if last hit note is a strum (for charts like Zoidberg the Cowboy by schmutz06)
				if (latestInputIsStrum && lastHitNote is not null && !lastHitNote[0].hopo && !lastHitNote[0].tap) {
					latestInputIsStrum = false;
				} else {
					// Input is valid; clear it to avoid multi-hit later
					latestInput = null;
				}
			}

			// If correct chord is pressed, and is not a multi-hit, hit it!
			expectedHits.Dequeue();

			ResetAllowedChordGhosts();
			Combo++;
			notesHit++;
			strummedCurrentNote = strummedCurrentNote || strummed || strumLeniency > 0f;
			strumLeniency = 0f;
			StopAudio = false;
			lastHitNote = chord;

			// Solo stuff
			if (soloInProgress) {
				soloNotesHit++;
			}

			foreach (var hit in chord) {
				hitChartIndex++;
				// Hit notes
				notePool.HitNote(hit);

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
					if (hit.fret < 5) frets[hit.fret].PlaySustainParticles(); // TEMP (remove check later)
					scoreKeeper.Add(susTracker.Strum(hit) * Multiplier * SUSTAIN_PTS_PER_BEAT);
					if (hit.fret < 5) frets[hit.fret].PlayAnimationSustainsLooped(); // TEMP (remove check later)

					// Check if it's extended sustain;
					var nextNote = GetNextNote(hit.time);
					if (nextNote != null) {
						extendedSustain[hit.fret] = hit.EndTime > nextNote[0].time;
					}
				} else {
					extendedSustain[hit.fret] = false;
				}

				// Add stats
				scoreKeeper.Add(PTS_PER_NOTE * Multiplier);
			}

			// If this is a tap note, and it was hit without strumming,
			// add it to the allowed overstrums. This is so the player
			// doesn't lose their combo when they strum AFTER they hit
			// the tap note.
			if ((chord[0].hopo || chord[0].tap) && !strummedCurrentNote) {
				//allowedOverstrums.Clear();
				allowedOverstrums.Add(chord);
			} else if (allowedOverstrums.Count > 0 && !chord[0].hopo && !chord[0].tap) {
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
			while (allowedOverstrums.Count > 0 && HitMarginEndTime > allowedOverstrums[0][0].time) {
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
			StopAudio = true;
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];

				notePool.MissNote(heldNote);

				heldNotes.RemoveAt(i);
				susTracker.Drop(heldNote);
				if (heldNote.fret < 5) frets[heldNote.fret].StopAnimation(); // TEMP (remove check later)
				if (heldNote.fret < 5) frets[heldNote.fret].StopSustainParticles(); // TEMP (remove check later)
				extendedSustain[heldNote.fret] = false;
			}

			whammyLastNote = false;
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
						if (overlap && heldNotes.Any(j => j.fret == i)) {
							continue;
						}
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
							(chordList[0].hopo || chordList[0].tap) && !(strummed || strumLeniency > 0f || overstrumCheck) &&
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

		protected override void PauseToggled(bool pause) {
			if (!pause) {
				input.FretChangeEvent += FretChangedAction;
				input.StrumEvent += StrumAction;

				// Release any held frets
				for (int i = 0; i < 5; i++) {
					FretChangedAction(false, i);
				}
			} else {
				input.FretChangeEvent -= FretChangedAction;
				input.StrumEvent -= StrumAction;
			}
		}

		private void FretChangedAction(bool pressed, int fret) {
			latestInput = CurrentTime;
			latestInputIsStrum = false;

			// Should it check ghosting?
			if (SettingsManager.Settings.AntiGhosting.Data && allowedGhosts > 0 && pressed && hitChartIndex > 0) {
				bool checkGhosting = true;
				if (Constants.ALLOW_DESC_GHOSTS) {
					for (var i = 0; i < 5; i++) {
						if (i == fret) {
							continue;
						}
						if (frets[i].IsPressed) {
							if (fret < i) { // Don't check ghosting if pressed fret is below currently held fret
								checkGhosting = false;
								break;
							}
						}
					}
				}
				if (checkGhosting) {
					var nextNote = GetNextNote(Chart[hitChartIndex - 1].time);
					if ((nextNote == null || (!nextNote[0].hopo && !nextNote[0].tap)) ||
					(Constants.ALLOW_GHOST_IF_NO_NOTES && nextNote[0].time - CurrentTime > HitMarginFront * Constants.ALLOW_GHOST_IF_NO_NOTES_THRESHOLD)) {
						checkGhosting = false;
					}
					if (checkGhosting) {
						if (nextNote.Count == 1 && fret != nextNote[0].fret) { // Hitting wrong button = ghosted = bad
							allowedGhosts--;
						}
						if (nextNote.Count > 1) { // If chord...
							if (allowedChordGhosts[fret] == 1) { // Fret is part of chord, and hasn't been ghosted yet
								allowedChordGhosts[fret] = 0;
							} else { // Actual ghost input
								allowedGhosts--;
							}
						}
					}
				}
			}

			frets[fret].SetPressed(pressed);

			if (pressed) {
				// Let go of held notes if wrong note pressed
				if (!IsExtendedSustain()) { // Unless it's extended sustains
					bool release = false;
					//
					for (int i = heldNotes.Count - 1; i >= 0; i--) {
						var heldNote = heldNotes[i];
						if (heldNote.fret < 5 && (heldNote.fret == fret || (heldNotes.Count == 1 && fret < heldNote.fret))) { // Button press is valid
							continue;
						} else { // Wrong button pressed; release all sustains
							release = true;
							break;
						}
					}
					if (release) { // Actually release all sustains
						StopAudio = true;
						for (int i = heldNotes.Count - 1; i >= 0; i--) {
							var heldNote = heldNotes[i];
							notePool.MissNote(heldNote);
							heldNotes.RemoveAt(i);
							if (heldNote.fret < 5) frets[heldNote.fret].StopAnimation(); // TEMP (remove check later)
							if (heldNote.fret < 5) frets[heldNote.fret].StopSustainParticles(); // TEMP (remove check later)
							extendedSustain[heldNote.fret] = false;
						}

						whammyLastNote = false;
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
					if (heldNote.fret < 5) frets[heldNote.fret].StopAnimation(); // TEMP (remove check later)
					if (heldNote.fret < 5) frets[heldNote.fret].StopSustainParticles(); // TEMP (remove check later)
					extendedSustain[heldNote.fret] = false;

					letGo = heldNote;

					whammyLastNote = false;
				}

				// Only stop audio if all notes were let go and...
				if (letGo != null && heldNotes.Count <= 0) {
					// ...if the player let go of the note more than 0.15s
					// before the end of the note. This prevents the game
					// from stopping the audio if the player let go a handful
					// of milliseconds too early (which is okay).
					float endTime = letGo.time + letGo.length;
					if (endTime - CurrentTime > 0.15f) {
						StopAudio = true;
					}
				}
			}
		}

		private void StrumAction() {
			latestInput = CurrentTime;
			latestInputIsStrum = true;

			// Strum leniency already active and another strum inputted, a double strum occurred (must overstrum)
			if (strumLeniency > 0f) {
				UpdateOverstrums();
			}

			strummed = true;
			if (!input.BotMode) {
				strumLeniency = Constants.STRUM_LENIENCY;
			}
		}

		private void WhammyEvent(float delta) {
			whammyAmount += Mathf.Abs(delta) * 0.25f;
			whammyAmount = Mathf.Clamp(whammyAmount, 0f, 1f / 3f);
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			float x = noteInfo.fret == 5 ? 0f : frets[noteInfo.fret].transform.localPosition.x;
			var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			// Get model type
			var model = NoteComponent.ModelType.NOTE;
			if (noteInfo.fret == 5) {
				model = noteInfo.hopo || noteInfo.tap
					? NoteComponent.ModelType.FULL_HOPO
					: NoteComponent.ModelType.FULL;
			} else if (noteInfo.hopo) {
				model = NoteComponent.ModelType.HOPO;
			} else if (noteInfo.tap) {
				model = NoteComponent.ModelType.TAP;
			}

			// Set note info
			var noteComp = notePool.AddNote(noteInfo, pos);
			startFCDetection = true;
			noteComp.SetInfo(
				noteInfo,
				commonTrack.NoteColor(noteInfo.fret),
				commonTrack.SustainColor(noteInfo.fret),
				noteInfo.length,
				model,
				noteInfo.time >= CurrentVisualStarpower?.time && noteInfo.time < CurrentVisualStarpower?.EndTime
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

		private List<NoteInfo> GetNextNote(float currentChordTime) {
			var i = hitChartIndex;
			List<NoteInfo> chord = new();
			while (Chart.Count > i) {
				if (Chart[i].time > currentChordTime) {
					var nextChordTime = Chart[i].time;
					chord.Add(Chart[i]);
					i++;
					while (Chart.Count > i) {
						if (Chart[i].time > nextChordTime) {
							break;
						} else {
							chord.Add(Chart[i]);
							i++;
						}
					}
					break;
				}
				i++;
			}
			if (chord.Count == 0) {
				return null;
			} else {
				// If it's a chord, set allowed ghosts accordingly
				if (chord.Count > 1) {
					foreach (NoteInfo hit in chord) {
						if (allowedChordGhosts[hit.fret] == -1) {
							allowedChordGhosts[hit.fret] = 1;
						}
					}
				} else {
					ResetAllowedChordGhosts(false);
				}
				return chord;
			}
		}

		private void ResetAllowedChordGhosts(bool resetGhosts = true) {
			if (resetGhosts) {
				allowedGhosts = allowedGhostsDefault;
			}
			for (var i = 0; i < 5; i++) {
				allowedChordGhosts[i] = -1;
			}
		}

		private bool IsExtendedSustain() {
			return extendedSustain.Any(x => x);
		}

		public override int GetChartCount() {
			if (noteCount > -1) {
				return noteCount;
			}
			int count = 0;
			for (int i = 0; i < Chart.Count; i++) {
				if (i == 0 || Chart[i].time > Chart[i - 1].time) {
					count++;
				}
			}
			return count;
		}
	}
}