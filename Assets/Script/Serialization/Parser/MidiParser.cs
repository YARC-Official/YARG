using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;
using YARG.Data;
using YARG.DiffDownsample;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private const bool FORCE_DOWNSAMPLE = false;

		private struct EventIR {
			public long startTick;
			public long? endTick;
			public string name;
		}

		public MidiFile midi;

		public MidiParser(SongInfo songInfo, string[] files) : base(songInfo, files) {
			midi = MidiFile.Read(files[0]);

			// Merge midi files
			for (int i = 1; i < files.Length; i++) {
				var upgrade = MidiFile.Read(files[i]);

				foreach (var trackChunk in upgrade.GetTrackChunks()) {
					foreach (var trackEvent in trackChunk.Events) {
						if (trackEvent is not SequenceTrackNameEvent trackName) {
							continue;
						}

						// Only merge specific tracks
						switch (trackName.Text) {
							case "PART REAL_GUITAR":
							case "PART REAL_BASS":
								midi.Chunks.Add(trackChunk);
								break;
						}
					}
				}
			}
		}

		public override void Parse(Chart chart) {
			var eventIR = new List<EventIR>();
			var tempo = midi.GetTempoMap();

			foreach (var trackChunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in trackChunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					// Parse each chunk
					try {
						switch (trackName.Text) {
							case "PART GUITAR":
								for (int i = 0; i < 4; i++) {
									chart.guitar[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "guitar");
								break;
							case "PART BASS":
								for (int i = 0; i < 4; i++) {
									chart.bass[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "bass");
								break;
							case "PART KEYS":
								for (int i = 0; i < 4; i++) {
									chart.keys[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "keys");
								break;
							case "PART VOCALS":
								chart.genericLyrics = ParseGenericLyrics(trackChunk, tempo);
								chart.realLyrics = ParseRealLyrics(eventIR, trackChunk, tempo);
								ParseStarpower(eventIR, trackChunk, "vocals");
								break;
							case "PART REAL_GUITAR":
								for (int i = 0; i < 4; i++) {
									chart.realGuitar[i] = ParseRealGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "realGuitar");
								break;
							case "PART REAL_BASS":
								for (int i = 0; i < 4; i++) {
									chart.realBass[i] = ParseRealGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "realBass");
								break;
							case "PART DRUM":
							case "PART DRUMS":
								for (int i = 0; i < 5; i++) {
									chart.drums[i] = ParseDrums(trackChunk, i);
									chart.ghDrums[i] = ParseGHDrums(trackChunk, i);
								}
								break;
							case "BEAT":
								ParseBeats(eventIR, trackChunk);
								break;
						}
					} catch (Exception e) {
						Debug.LogError($"Error while parsing track chunk named `{trackName.Text}`. Skipped.");
						Debug.LogException(e);
					}
				}
			}

			// Downsample instruments

			foreach (var subChart in chart.allParts) {
				try {
					// Downsample Five Fret instruments
					if (subChart == chart.guitar || subChart == chart.bass || subChart == chart.keys) {
						if (subChart[3].Count >= 1 && (subChart[2].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[2] = FiveFretDownsample.DownsampleExpertToHard(subChart[3]);
							Debug.Log("Downsampled expert to hard.");
						}

						if (subChart[2].Count >= 1 && (subChart[1].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[1] = FiveFretDownsample.DownsampleHardToMedium(subChart[2]);
							Debug.Log("Downsampled hard to normal.");
						}

						if (subChart[1].Count >= 1 && (subChart[0].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[0] = FiveFretDownsample.DownsampleMediumToEasy(subChart[1]);
							Debug.Log("Downsampled normal to easy.");
						}
					}
				} catch (Exception e) {
					Debug.LogError("Error while downsampling. Skipped.");
					Debug.LogException(e);
				}
			}

			// Sort notes by time (just in case) and add delay

			float lastNoteTime = 0f;
			foreach (var part in chart.allParts) {
				foreach (var difficulty in part) {
					if (difficulty == null || difficulty.Count <= 0) {
						continue;
					}

					// Sort
					difficulty.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));

					// Add delay
					foreach (var note in difficulty) {
						note.time += songInfo.delay;
					}

					// Last note time
					if (difficulty[^1].EndTime > lastNoteTime) {
						lastNoteTime = difficulty[^1].EndTime;
					}
				}
			}

			// Add delay to vocals

			foreach (var lyric in chart.genericLyrics) {
				lyric.time += songInfo.delay;
			}

			foreach (var lyric in chart.realLyrics) {
				lyric.time += songInfo.delay;
			}

			// Generate beat line events if there aren't any

			if (!eventIR.Any(i => i.name == "beatLine_minor" || i.name == "beatLine_major")) {
				GenerateBeats(eventIR, tempo, lastNoteTime);
			}

			// Convert event IR into real

			chart.events = new();

			foreach (var eventInfo in eventIR) {
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.startTick, tempo).TotalSeconds;

				// If the event has length, do that too
				if (eventInfo.endTick.HasValue) {
					float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.endTick.Value, tempo).TotalSeconds;
					chart.events.Add(new EventInfo(eventInfo.name, startTime, endTime - startTime));
				} else {
					chart.events.Add(new EventInfo(eventInfo.name, startTime));
				}
			}

			// Sort events by time (required) and add delay

			chart.events.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
			foreach (var ev in chart.events) {
				ev.time += songInfo.delay;
			}

			// Add beats to chart

			chart.beats = new();
			foreach (var ev in chart.events) {
				if (ev.name == "beatLine_minor" || ev.name == "beatLine_major") {
					chart.beats.Add(ev.time);
				}
			}

			// Look for bonus star power

			// TODO
		}

		private void ParseStarpower(List<EventIR> eventIR, TrackChunk trackChunk, string instrument) {
			long totalDelta = 0;

			long? starPowerStart = null;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != 8) {
					continue;
				}

				// Skip if not a star power event
				if (noteEvent.GetNoteName() != NoteName.GSharp) {
					continue;
				}

				if (trackEvent is NoteOnEvent) {
					// We need to know when it ends before adding it
					starPowerStart = totalDelta;
				} else if (trackEvent is NoteOffEvent) {
					if (starPowerStart == null) {
						continue;
					}

					// Now that we know the start and end, add it to the list of events.
					eventIR.Add(new EventIR {
						startTick = starPowerStart.Value,
						endTick = totalDelta,
						name = $"starpower_{instrument}"
					});
					starPowerStart = null;
				}
			}
		}
	}
}