using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Chart;
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
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;
		[SerializeField]
		private ParticleGroup kickNoteParticles;
		[SerializeField]
		private MeshRenderer kickFretInside;
		[SerializeField]
		private Animation kickFretAnimation;
		[SerializeField]
		public bool shakeOnKick = true;

		private Queue<List<NoteInfo>> expectedHits = new();

		private readonly string[] proInst = { "realDrums", "ghDrums" };

		private int ptsPerNote;

		protected override void StartTrack() {
			notePool.player = player;
			genericPool.player = player;

			// Inputs

			input = player.inputStrategy;
			input.ResetForSong();

			if (input is DrumsInputStrategy drumStrat) {
				drumStrat.DrumHitEvent += DrumHitAction;
			} else if (input is GHDrumsInputStrategy ghStrat) {
				ghStrat.DrumHitEvent += GHDrumHitAction;
			}

			if (input.botMode) {
				input.InitializeBotMode(Chart);
			}

			// GH vs RB

			kickIndex = fiveLaneMode ? 5 : 4;

			// Lefty flip

			if (player.leftyFlip) {
				drums = drums.Reverse().ToArray();
				// Make the drum colors follow the original order even though the chart is flipped
				Array.Reverse(commonTrack.colorMappings, 0, kickIndex);
			}

			// Color drums
			for (int i = 0; i < drums.Length; i++) {
				var fret = drums[i].GetComponent<Fret>();
				fret.SetColor(commonTrack.FretColor(i), commonTrack.FretInnerColor(i), commonTrack.SustainColor(i));
				drums[i] = fret;
			}
			kickNoteParticles.Colorize(commonTrack.FretColor(kickIndex));

			// Color Kick Frets
			kickFretInside.material.color = (commonTrack.FretColor(kickIndex));
			kickFretInside.material.SetColor("_EmissionColor", commonTrack.FretColor(kickIndex) * 2);

			// initialize scoring variables
			ptsPerNote = proInst.Contains(player.chosenInstrument) ? 30 : 25;
			starsKeeper = new(Chart, scoreKeeper,
				player.chosenInstrument,
				ptsPerNote);
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			// Unbind input
			if (input is DrumsInputStrategy drumStrat) {
				drumStrat.DrumHitEvent -= DrumHitAction;
			} else if (input is GHDrumsInputStrategy ghStrat) {
				ghStrat.DrumHitEvent -= GHDrumHitAction;
			}
		}

		protected override void UpdateTrack() {

			
			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			var events = Play.Instance.chart.events;
			var beats = Play.Instance.chart.beats;

			// Update events (beat lines, starpower, etc.)
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= RelativeTime) {
				var eventInfo = events[eventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				// if (eventInfo.name == "beatLine_minor") {
				// 	genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				// } else if (eventInfo.name == "beatLine_major") {
				// 	genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				if (eventInfo.name == $"starpower_{player.chosenInstrument}") {
					StarpowerSection = eventInfo;
				} else if (eventInfo.name == $"fill_{player.chosenInstrument}") {
					FillSection = eventInfo;
				} else if (eventInfo.name == $"solo_{player.chosenInstrument}") {
					SoloSection = eventInfo;
				}
				eventChartIndex++;
			}
			
			while (beats.Count > beatChartIndex && beats[beatChartIndex].Time <= RelativeTime) {
				var beatInfo = beats[beatChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, beatInfo.Time);
				if (beatInfo.Style is BeatStyle.STRONG or BeatStyle.WEAK) {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				} else if (beatInfo.Style == BeatStyle.MEASURE) {
					genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				}
				beatChartIndex++;
			}

			// Since chart is sorted, this is guaranteed to work
			while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= RelativeTime) {
				var noteInfo = Chart[visualChartIndex];

				// Skip kick notes if noKickMode is enabled
				if (noteInfo.fret == kickIndex && SettingsManager.Settings.NoKicks.Data) {
					visualChartIndex++;
					continue;
				}

				// TODO: Only one note should be an activator at any given timestamp
				if (player.track.FillSection?.EndTime == noteInfo.time
					&& starpowerCharge >= 0.5f
					&& !IsStarPowerActive

					) {
					noteInfo.isActivator = true;
				}

				SpawnNote(noteInfo, RelativeTime);
				visualChartIndex++;
			}

			// Update expected input
			while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= Play.Instance.SongTime + Constants.HIT_MARGIN) {
				var noteInfo = Chart[inputChartIndex];

				// Skip kick notes if noKickMode is enabled
				if (noteInfo.fret == kickIndex && SettingsManager.Settings.NoKicks.Data) {
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

		public override void SetReverb(bool on) {
			Play.Instance.ReverbAudio("drums", on);
			Play.Instance.ReverbAudio("drums_1", on);
			Play.Instance.ReverbAudio("drums_2", on);
			Play.Instance.ReverbAudio("drums_3", on);
			Play.Instance.ReverbAudio("drums_4", on);
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Play.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Constants.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				foreach (var hit in missedChord) {
					hitChartIndex++;

					// The player should not be penalized for missing activator notes
					if (hit.isActivator) {
						continue;
					}

					Combo = 0;
					missedAnyNote = true;
					notePool.MissNote(hit);
					StopAudio = true;
				}
			}
		}

		protected override void PauseToggled(bool pause) {
			if (!pause) {
				if (input is DrumsInputStrategy drumStrat) {
					drumStrat.DrumHitEvent += DrumHitAction;
				} else if (input is GHDrumsInputStrategy ghStrat) {
					ghStrat.DrumHitEvent += GHDrumHitAction;
				}
			} else {
				if (input is DrumsInputStrategy drumStrat) {
					drumStrat.DrumHitEvent -= DrumHitAction;
				} else if (input is GHDrumsInputStrategy ghStrat) {
					ghStrat.DrumHitEvent -= GHDrumHitAction;
				}
			}
		}

		private void GHDrumHitAction(int drum) {
			DrumHitAction(drum, false);
		}

		private void DrumHitAction(int drum, bool cymbal) {
			// invert input in case lefty flip is on, bots don't need it
			if (player.leftyFlip && !input.botMode) {
				switch (drum) {
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
						// lefty flip on pro drums means physically moving the green cymbal above the red snare
						// so while the position on the chart has changed, the input object is the same
						if (!cymbal) {
							drum = kickIndex == 4 ? 0 : 1;
						}
						break;
					case 4:
						if (kickIndex == 5) {
							drum = 0;
						}
						break;
				}
			}

			if (drum != kickIndex) {
				// Hit effect
				drums[drum].PlayAnimationDrums();
				drums[drum].Pulse();
			} else {
				PlayKickFretAnimation();

				if (shakeOnKick) {
					//commonTrack.PlayKickCameraAnimation();
					trackAnims.PlayKickShakeCameraAnim();
				}

				commonTrack.kickFlash.SetActive(true);
				trackAnims.PlayKickFlashAnim();
			}

			// Overstrum if no expected
			if (expectedHits.Count <= 0) {
				Combo = 0;

				return;
			}

			// Handle hits (one per frame so no double hits)
			var notes = expectedHits.Peek();

			// Check if a drum was hit
			NoteInfo hit = null;
			foreach (var note in notes) {
				// Check if correct cymbal was hit
				bool cymbalHit = note.hopo == cymbal;
				if (player.chosenInstrument == "drums") {
					cymbalHit = true;
				}
				// Check if correct drum was hit
				if (note.fret == drum && cymbalHit) {
					hit = note;
					if (note.isActivator) {
						(input as DrumsInputStrategy).ActivateStarpower();
					}
					break;
				}
			}

			// "Overstrum" (or overhit in this case)
			if (hit == null) {
				Combo = 0;

				return;
			}

			// If so, hit! (Remove from "chord")
			// bool lastNote = false;
			notes.RemoveAll(i => i.fret == drum);
			if (notes.Count <= 0) {
				//lastNote = true;  //  <-- This comment (disable) on the line is a solution for drum notes stop being counted as "chords" and being clumped together, which shouldn't happen. -Mia
				expectedHits.Dequeue();
			}

			// Activators should not affect combo
			if (!hit.isActivator) {
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
				
				kickNoteParticles.Stop();
				kickNoteParticles.Play();
			}

			// Add stats
			notesHit++;
			// TODO: accomodate for disabled cymbal lanes, rework 5-lane scoring depending on re-charting
			scoreKeeper.Add(Multiplier * ptsPerNote);
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
				SettingsManager.Settings.UseCymbalModelsInFiveLane.Data) {

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
			startFCDetection = true;
			noteComp.SetInfo(
				commonTrack.NoteColor(noteInfo.fret),
				commonTrack.SustainColor(noteInfo.fret),
				noteInfo.length,
				model,
				noteInfo.isActivator
			);
		}

		private void PlayKickFretAnimation() {
			StopKickFretAnimation();

			kickFretAnimation["KickFrets"].wrapMode = WrapMode.Once;
			kickFretAnimation.Play("KickFrets");
		}

		private void StopKickFretAnimation() {
			kickFretAnimation.Stop();
			kickFretAnimation.Rewind();
		}
	}
}