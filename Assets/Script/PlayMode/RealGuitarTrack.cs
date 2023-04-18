using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Util;
using static YARG.Input.RealGuitarInputStrategy;

namespace YARG.PlayMode {
	public class RealGuitarTrack : AbstractTrack {
		private StrumFlag strumFlag = StrumFlag.NONE;
		private int[] fretCache = new int[6];

		private RealGuitarInputStrategy input;

		[Space]
		[SerializeField]
		private TextMeshPro[] fretNumbers;
		[SerializeField]
		private ParticleGroup[] hitParticles;
		[SerializeField]
		private ParticleGroup[] sustainParticles;
		[SerializeField]
		private Color[] stringColors;
		[SerializeField]
		private Color[] noteColors;
		[SerializeField]
		private Color[] sustainColors;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;

		private Queue<NoteInfo> expectedHits = new();
		private List<NoteInfo> heldNotes = new();

		private int notesHit = 0;
		private int notesMissed = 0;

		protected override void StartTrack() {
			notePool.player = player;
			genericPool.player = player;

			// Lefty flip (TODO)

			// if (player.leftyFlip) {
			// 	fretNumbers = fretNumbers.Reverse().ToArray();
			// 	hitParticles = hitParticles.Reverse().ToArray();
			// 	sustainParticles = sustainParticles.Reverse().ToArray();
			// 	stringColors = stringColors.Reverse().ToArray();
			// }

			// Inputs

			input = (RealGuitarInputStrategy) player.inputStrategy;
			input.ResetForSong();

			input.FretChangeEvent += FretChangedAction;
			input.StrumEvent += StrumAction;

			// Color particles

			for (int i = 0; i < 6; i++) {
				hitParticles[i].Colorize(stringColors[i]);
			}

			for (int i = 0; i < 6; i++) {
				sustainParticles[i].Colorize(stringColors[i]);
			}
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
				expectedHits.Enqueue(Chart[inputChartIndex]);

				inputChartIndex++;
			}

			// Update held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];
				if (heldNote.time + heldNote.length <= Play.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					EndSustainParticles(heldNote);
				}
			}

			UpdateInput();

			strumFlag = StrumFlag.NONE;
		}

		public override void SetReverb(bool on) {
			Play.Instance.ReverbAudio("guitar", on);
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?.time > Constants.HIT_MARGIN) {
				var missedNote = expectedHits.Dequeue();

				// Call miss for each component
				hitChartIndex++;
				missedAnyNote = true;
				Combo = 0;
				notePool.MissNote(missedNote);
				StopAudio = true;
			}


			if (expectedHits.Count <= 0) {
				// UpdateOverstrums();
				return;
			}

			// Handle hits (one per frame so no double hits)
			var note = expectedHits.Peek();
			if (!note.hopo && strumFlag == StrumFlag.NONE) {
				return;
			} else if (note.hopo && Combo <= 0 && strumFlag == StrumFlag.NONE) {
				return;
			}

			// Check if correct chord is pressed
			if (!NotePressed(note) || !NoteStrummed(note)) {
				// UpdateOverstrums();

				if (!note.hopo) {
					Combo = 0;
				}

				return;
			}

			// If so, hit!
			hitChartIndex++;
			expectedHits.Dequeue();

			Combo++;

			// Hit notes
			notePool.HitNote(note);
			StopAudio = false;
			notesHit++;

			// Solo stuff
			if (Play.Instance.SongTime >= SoloSection?.time && Play.Instance.SongTime <= SoloSection?.EndTime) {
				soloNotesHit++;
			} else if (Play.Instance.SongTime >= SoloSection?.EndTime + 10) {
				soloNotesHit = 0;
			}

			// Play particles
			for (int i = 0; i < 6; i++) {
				if (note.stringFrets[i] == -1) {
					continue;
				}

				hitParticles[i].Play();
			}

			// If sustained, add to held
			if (note.length > 0.2f) {
				heldNotes.Add(note);
				StartSustainParticles(note);
			}
		}

		private bool NoteStrummed(NoteInfo note) {
			int extras = 0;
			for (int str = 0; str < note.stringFrets.Length; str++) {
				bool has = strumFlag.HasFlag(StrumFlagFromInt(str));

				if (note.stringFrets[str] == -1) {
					if (has) {
						extras++;
					}

					continue;
				}

				if (!has) {
					return false;
				}
			}

			// Ignore extras for now
			// return extras <= 3;
			return true;
		}

		private bool NotePressed(NoteInfo note) {
			if (note.muted) {
				// It seems like this is just how they work...
				return true;
			}

			for (int str = 0; str < note.stringFrets.Length; str++) {
				if (note.stringFrets[str] == -1) {
					continue;
				}

				if (note.stringFrets[str] != fretCache[str]) {
					return false;
				}
			}

			return true;
		}

		private void StrumAction(StrumFlag str) {
			strumFlag = str;
		}

		private void FretChangedAction(int str, int fret) {
			fretCache[str] = fret;

			if (fret == 0) {
				fretNumbers[str].text = "";
			} else {
				fretNumbers[str].text = fret.ToString();
			}
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			for (int i = 0; i < 6; i++) {
				if (noteInfo.stringFrets[i] == -1) {
					continue;
				}

				// Set correct position
				float x = fretNumbers[i].transform.localPosition.x;
				var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

				// Set note info
				var noteComp = notePool.AddNote(noteInfo, pos);
				startFCDetection = true;
				var model = noteInfo.hopo ? NoteComponent.ModelType.HOPO : NoteComponent.ModelType.NOTE;
				noteComp.SetInfo(noteColors[i], sustainColors[i], noteInfo.length, model);
				noteComp.SetFretNumber(noteInfo.muted ? "X" : noteInfo.stringFrets[i].ToString());
			}
		}

		private void StartSustainParticles(NoteInfo note) {
			for (int i = 0; i < 6; i++) {
				if (note.stringFrets[i] == -1) {
					continue;
				}

				sustainParticles[i].Play();
			}
		}

		private void EndSustainParticles(NoteInfo note) {
			for (int i = 0; i < 6; i++) {
				if (note.stringFrets[i] == -1) {
					continue;
				}

				sustainParticles[i].Stop();
			}
		}
	}
}