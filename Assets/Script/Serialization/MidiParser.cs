using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;

namespace YARG.Serialization {
	public class MidiParser : AbstractParser {
		private enum ForceState {
			NONE,
			HOPO,
			AUTO_HOPO,
			STRUM
		}

		private struct EventIR {
			public long startTick;
			public string name;
		}

		private struct NoteIR {
			public long startTick;
			public long endTick;
			public int fret;

			public ForceState forceState;
			public bool isChord;
		}

		public MidiFile midi;

		public MidiParser(string file) : base(file) {
			midi = MidiFile.Read(file);
		}

		public override void Parse(Chart chart) {
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
									chart.guitar[i] = ParseGuitar(trackChunk, i);
								}
								break;
							case "PART BASS":
								for (int i = 0; i < 4; i++) {
									chart.bass[i] = ParseGuitar(trackChunk, i);
								}
								break;
							case "PART KEYS":
								for (int i = 0; i < 4; i++) {
									chart.keys[i] = ParseGuitar(trackChunk, i);
								}
								break;
							case "BEAT":
								chart.events = ParseBeats(trackChunk);
								break;
						}
					} catch (Exception e) {
						Debug.LogError($"Error while parsing track chunk named `{trackName.Text}`. Skipped.");
						Debug.LogException(e);
					}
				}
			}

			// Sort by time (just in case)
			foreach (var part in chart.allParts) {
				foreach (var difficulty in part) {
					difficulty?.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
				}
			}
			chart.events?.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
		}

		private List<NoteInfo> ParseGuitar(TrackChunk trackChunk, int difficulty) {
			var tempo = midi.GetTempoMap();

			long totalDelta = 0;

			var noteIR = new List<NoteIR>();
			long?[] fretState = new long?[5];
			var forceState = ForceState.NONE;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Expert octave
				if (noteEvent.GetNoteOctave() != 4 + difficulty) {
					continue;
				}

				// Convert note to fret number (or special)
				int fretNum = noteEvent.GetNoteName() switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					NoteName.D => 2,
					NoteName.DSharp => 3,
					NoteName.E => 4,
					NoteName.F => 5,
					NoteName.FSharp => 6,
					_ => -1
				};

				// Skip if not an actual note
				if (fretNum == -1) {
					continue;
				}

				// Handle special
				if (fretNum == 5) {
					forceState = noteEvent is NoteOnEvent ? ForceState.HOPO : ForceState.NONE;
					continue;
				} else if (fretNum == 6) {
					forceState = noteEvent is NoteOnEvent ? ForceState.STRUM : ForceState.NONE;
					continue;
				}

				// Deal with note ons or note offs

				// TODO: Fix invalid HOPOs after chord. See "In the Meantime"

				if (fretState[fretNum] != null) {
					var noteForceState = forceState;
					bool isChord = false;

					if (noteForceState == ForceState.NONE && noteIR.Count > 0) {
						var lastNote = noteIR[^1];

						// Check for chord
						if (lastNote.startTick == fretState[fretNum].Value) {
							isChord = true;

							// Check for missing first note of chord
							if (!lastNote.isChord) {
								lastNote.isChord = true;
								lastNote.forceState = lastNote.forceState == ForceState.AUTO_HOPO ?
									ForceState.NONE :
									lastNote.forceState;

								noteIR[^1] = lastNote;
							}
						}

						// Check for auto HOPO and chord
						if (lastNote.fret != fretNum && !isChord) {
							// Wrap in a try just in case noteBeat < lastNoteBeat
							try {
								var lastNoteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(lastNote.startTick, tempo);
								var noteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(fretState[fretNum].Value, tempo);
								var distance = noteBeat - lastNoteBeat;

								// Thanks?? https://tcrf.net/Proto:Guitar_Hero
								// According to this, auto-HOPO threshold is 170 ticks.
								// "But a tick is different in every midi file!"
								// It also mentions that 160 is a twelth note.
								// 160 * 12 = 1920
								if (distance <= new MusicalTimeSpan(170, 1920)) {
									noteForceState = ForceState.AUTO_HOPO;
								}
							} catch { }
						}
					}

					noteIR.Add(new NoteIR {
						startTick = fretState[fretNum].Value,
						endTick = totalDelta,
						fret = fretNum,
						forceState = noteForceState,
						isChord = isChord
					});
				}

				if (noteEvent is NoteOnEvent) {
					fretState[fretNum] = totalDelta;
				} else {
					fretState[fretNum] = null;
				}
			}

			var noteOutput = new List<NoteInfo>(noteIR.Count);

			// IR into real
			foreach (var noteInfo in noteIR) {
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.startTick, tempo).TotalSeconds;
				float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.endTick, tempo).TotalSeconds;

				bool hopo = noteInfo.forceState == ForceState.HOPO
					|| noteInfo.forceState == ForceState.AUTO_HOPO;
				noteOutput.Add(new NoteInfo(startTime, noteInfo.fret, endTime - startTime, hopo));
			}

			return noteOutput;
		}

		private List<EventInfo> ParseBeats(TrackChunk trackChunk) {
			var eventIR = new List<EventIR>();
			long totalDelta = 0;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteOnEvent noteOnEvent) {
					continue;
				}

				// Convert note to beat line type
				int majorOrMinor = noteOnEvent.GetNoteName() switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					_ => -1
				};

				// Skip if not a beat line
				if (majorOrMinor == -1) {
					continue;
				}

				if (majorOrMinor == 1) {
					eventIR.Add(new EventIR {
						startTick = totalDelta,
						name = "beatLine_minor"
					});
				} else {
					eventIR.Add(new EventIR {
						startTick = totalDelta,
						name = "beatLine_major"
					});
				}
			}

			var eventOutput = new List<EventInfo>(eventIR.Count);
			var tempo = midi.GetTempoMap();

			// IR into real
			foreach (var eventInfo in eventIR) {
				float time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.startTick, tempo).TotalSeconds;
				eventOutput.Add(new EventInfo(time, eventInfo.name));
			}

			return eventOutput;
		}
	}
}