using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Settings;
using YARG.Util;

namespace YARG.PlayMode {
	public sealed class DrumsTrack : AbstractTrack {
		private InputStrategy input;

		[Space]
		[SerializeField]
		private bool fiveLaneMode = false;

		private int kickIndex = 4;

		[Space]
		[SerializeField]
		private Fret[] drums;
		[SerializeField]
		private Color[] drumColors;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;
		[SerializeField]
		private ParticleGroup kickNoteParticles;

		private Queue<List<NoteInfo>> expectedHits = new();

		private int notesHit = 0;

		private bool noKickMode = false;

		private string[] proScoreInst = {"realDrums", "ghDrums"};

		protected override void StartTrack() {
			notePool.player = player;
			genericPool.player = player;

			noKickMode = SettingsManager.GetSettingValue<bool>("noKicks");

			// Inputs

			input = player.inputStrategy;
			input.ResetForSong();

			if (input is DrumsInputStrategy drumStrat) {
				drumStrat.DrumHitEvent += DrumHitAction;
			} else if (input is GHDrumsInputStrategy ghStrat) {
				ghStrat.DrumHitEvent += GHDrumHitAction;
			}

			// GH vs RB

			kickIndex = fiveLaneMode ? 5 : 4;
			
			// Lefty flip

			if (player.leftyFlip) {
				drums = drums.Reverse().ToArray();
				// Make the drum colors follow the original order even though the chart is flipped
				Array.Reverse(drumColors, 0, kickIndex);
			}

			// Color drums
			for (int i = 0; i < drums.Length; i++) {
				var fret = drums[i].GetComponent<Fret>();
				fret.SetColor(drumColors[i]);
				drums[i] = fret;
			}
			kickNoteParticles.Colorize(drumColors[kickIndex]);

			starKeeper = new(Chart, scoreKeeper,
				player.chosenInstrument,
				proScoreInst.Contains(player.chosenInstrument) ? 30 : 25);
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			if (input is DrumsInputStrategy drumStrat) {
				drumStrat.DrumHitEvent -= DrumHitAction;
			} else if (input is GHDrumsInputStrategy ghStrat) {
				ghStrat.DrumHitEvent -= GHDrumHitAction;
			}

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
				}

				eventChartIndex++;
			}

			// Since chart is sorted, this is guaranteed to work
			while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= RelativeTime) {
				var noteInfo = Chart[visualChartIndex];

				// Skip kick notes if noKickMode is enabled
				if (noteInfo.fret == kickIndex && noKickMode) {
					visualChartIndex++;
					continue;
				}

				SpawnNote(noteInfo, RelativeTime);
				visualChartIndex++;
			}

			// Update expected input
			while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= Play.Instance.SongTime + Constants.HIT_MARGIN) {
				var noteInfo = Chart[inputChartIndex];

				// Skip kick notes if noKickMode is enabled
				if (noteInfo.fret == kickIndex && noKickMode) {
					inputChartIndex++;
					continue;
				}

				var peeked = expectedHits.ReversePeekOrNull();
				if (peeked?[0].time == noteInfo.time) {
					// Add notes as chords
					peeked.Add(noteInfo);
				} else {
					// Or add notes as singular
					var l = new List<NoteInfo>(6) { noteInfo };
					expectedHits.Enqueue(l);
				}

				inputChartIndex++;
			}

			UpdateInput();
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Constants.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					hitChartIndex++;
					notePool.MissNote(hit);
					StopAudio = true;
				}
			}
		}

		private void GHDrumHitAction(int drum) {
			DrumHitAction(drum, false);
		}

		private void DrumHitAction(int drum, bool cymbal) {
			// invert input in case lefty flip is on, bots don't need it
			if (player.leftyFlip && !input.botMode){
				switch (drum){
					case 0:
						drum = kickIndex == 4 ? 3 : 4;
						break;
					case 1:
						drum = kickIndex == 4 ? 2 : 3;
						break;
					case 2:
						drum = kickIndex == 4 ? 1 : 2;
						break;
					case 3:
						drum = kickIndex == 4 ? 0 : 1;
						break;
					case 4:
						if (kickIndex == 5){
							drum = 0;
						}
						break;
				}
			}
			if (drum != kickIndex) {
				// Hit effect
				drums[drum].Pulse();
			}

			// Overstrum if no expected
			if (expectedHits.Count <= 0) {
				Combo = 0;

				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();

			// Check if a drum was hit
			NoteInfo hit = null;
			foreach (var note in chord) {
				// Check if correct cymbal was hit
				bool cymbalHit = note.hopo == cymbal;
				if (player.chosenInstrument == "drums") {
					cymbalHit = true;
				}

				// Check if correct drum was hit
				if (note.fret == drum && cymbalHit) {
					hit = note;
					break;
				}
			}

			// "Overstrum" (or overhit in this case)
			if (hit == null) {
				Combo = 0;

				return;
			}

			// If so, hit! (Remove from "chord")
			bool lastNote = false;
			chord.RemoveAll(i => i.fret == drum);
			if (chord.Count <= 0) {
				lastNote = true;
				expectedHits.Dequeue();
			}

			if (lastNote) {
				Combo++;
			}

			// Hit note
			hitChartIndex++;
			notePool.HitNote(hit);
			StopAudio = false;

			// Play particles
			if (hit.fret != kickIndex) {
				drums[hit.fret].PlayParticles();
			} else {
				kickNoteParticles.Play();
			}

			// Add stats
			notesHit++;

			// TODO: accomodate for disabled cymbal lanes; rework 5-lane scoring depending on re-charting
			scoreKeeper.Add(Multiplier * (proScoreInst.Contains(player.chosenInstrument) ? 30 : 25));
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			float x = noteInfo.fret == kickIndex ? 0f : drums[noteInfo.fret].transform.localPosition.x;
			var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			// Get model type
			var model = NoteComponent.ModelType.NOTE;
			if (noteInfo.fret == kickIndex) {
				// Kick
				model = NoteComponent.ModelType.FULL;
			} else if (player.chosenInstrument == "ghDrums" &&
				SettingsManager.GetSettingValue<bool>("useCymbalModelsInFiveLane")) {

				if (noteInfo.fret == 1 || noteInfo.fret == 3) {
					// Cymbal (only for gh-drums if enabled)
					model = NoteComponent.ModelType.HOPO;
				}
			} else {
				if (noteInfo.hopo && player.chosenInstrument == "realDrums") {
					// Cymbal (only for pro-drums)
					model = NoteComponent.ModelType.HOPO;
				}
			}

			// Set note info
			var noteComp = notePool.AddNote(noteInfo, pos);
			noteComp.SetInfo(drumColors[noteInfo.fret], noteInfo.length, model);
		}
	}
}