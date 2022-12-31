using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;
using YARG.Data;

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
			public long? endTick;
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

		public MidiParser(string file, float delay) : base(file, delay) {
			midi = MidiFile.Read(file);
		}

		public override void Parse(Chart chart) {
			var eventIR = new List<EventIR>();

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
								ParseStarpower(eventIR, trackChunk, "guitar");
								break;
							case "PART BASS":
								for (int i = 0; i < 4; i++) {
									chart.bass[i] = ParseGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "bass");
								break;
							case "PART KEYS":
								for (int i = 0; i < 4; i++) {
									chart.keys[i] = ParseGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "keys");
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

			// Convert event IR into real

			chart.events = new();
			var tempo = midi.GetTempoMap();

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
				ev.time += delay;
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

			// Sort notes by time (just in case) and add delay

			foreach (var part in chart.allParts) {
				foreach (var difficulty in part) {
					if (difficulty == null) {
						continue;
					}

					// Sort
					difficulty?.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));

					// Add delay
					foreach (var note in difficulty) {
						note.time += delay;
					}
				}
			}
		}

		private List<NoteInfo> ParseGuitar(TrackChunk trackChunk, int difficulty) {
			var tempo = midi.GetTempoMap();

			long totalDelta = 0;

			var noteIR = new List<NoteIR>();
			var forceState = ForceState.NONE;

			// Since each note has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the note. This stores
			// the ON event timings.
			long?[] fretState = new long?[5];

			// The last note that occured on each fret.
			// This is useful for HOPOs and such.
			long?[] lastNoteOnFret = new long?[5];

			// Ripple-down for last notes
			long?[] lastNoteOnFretRipple = new long?[5];

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != 4 + difficulty) {
					continue;
				}

				// Convert note to fret number (or special)
				int fretNum = noteEvent.GetNoteName() switch {
					// Green
					NoteName.C => 0,
					// Red
					NoteName.CSharp => 1,
					// Yellow
					NoteName.D => 2,
					// Blue
					NoteName.DSharp => 3,
					// Orange
					NoteName.E => 4,
					// Force HOPO
					NoteName.F => 5,
					// Force strum
					NoteName.FSharp => 6,
					_ => -1
				};

				// Skip if not an actual note
				if (fretNum == -1) {
					continue;
				}

				// Handle forces (on and off)
				if (fretNum == 5) {
					forceState = noteEvent is NoteOnEvent ? ForceState.HOPO : ForceState.NONE;
					continue;
				} else if (fretNum == 6) {
					forceState = noteEvent is NoteOnEvent ? ForceState.STRUM : ForceState.NONE;
					continue;
				}

				// Deal with notes
				if (noteEvent is NoteOnEvent) {
					// If it is a note on, wait until we get the note
					// off so we can get the length of the note.
					fretState[fretNum] = totalDelta;

					// Ripple-down last notes
					for (int i = 0; i < 5; i++) {
						lastNoteOnFret[i] = lastNoteOnFretRipple[i];
					}
					lastNoteOnFretRipple[fretNum] = totalDelta;
				} else if (noteEvent is NoteOffEvent) {
					// Here is were the notes are actually stored.
					// We now know the starting point and ending point.
					if (fretState[fretNum] != null) {
						var noteForceState = forceState;
						bool isChord = false;

						if (noteForceState == ForceState.NONE && noteIR.Count > 0) {
							var lastNote = noteIR[^1];

							// Check for chord
							if (lastNote.startTick == fretState[fretNum].Value) {
								isChord = true;

								// The first note of the chord doesn't know that it
								// is in a chord. Fix that, and chance HOPO status.
								if (!lastNote.isChord) {
									lastNote.isChord = true;
									lastNote.forceState = lastNote.forceState == ForceState.AUTO_HOPO ?
										ForceState.NONE :
										lastNote.forceState;

									noteIR[^1] = lastNote;
								}
							}

							// Check for auto HOPO
							if (!isChord) {
								// Get the MuscicalTimeSpan for the current note
								var noteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(fretState[fretNum].Value, tempo);

								for (int i = 0; i < 5; i++) {
									if (lastNoteOnFret[i] == null) {
										continue;
									}

									// Skip if there is a note in front of the last note on fret
									if (lastNoteOnFret[i] < lastNote.startTick) {
										continue;
									}

									// Wrap in a try just in case noteBeat < lastNoteBeat
									try {
										var lastNoteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(lastNoteOnFret[i].Value, tempo);
										var distance = noteBeat - lastNoteBeat;

										// Thanks?? https://tcrf.net/Proto:Guitar_Hero
										// According to this, auto-HOPO threshold is 170 ticks.
										// "But a tick is different in every midi file!"
										// It also mentions that 160 is a twelth note.
										// 160 * 12 = 1920
										if (distance <= new MusicalTimeSpan(170, 1920)) {
											if (i == fretNum) {
												noteForceState = ForceState.NONE;
												break;
											}
											noteForceState = ForceState.AUTO_HOPO;
										}
									} catch { }
								}
							}
						}

						// Add the note!
						noteIR.Add(new NoteIR {
							startTick = fretState[fretNum].Value,
							endTick = totalDelta,
							fret = fretNum,
							forceState = noteForceState,
							isChord = isChord
						});
					}

					// Reset fret state and wait until next ON event
					fretState[fretNum] = null;
				}
			}

			var noteOutput = new List<NoteInfo>(noteIR.Count);

			// Return empty list if no notes are available in this difficulty
			if (noteIR.Count <= 0) {
				return noteOutput;
			}

			// IR into real
			foreach (var noteInfo in noteIR) {
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.startTick, tempo).TotalSeconds;
				float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.endTick, tempo).TotalSeconds;

				bool hopo = noteInfo.forceState == ForceState.HOPO
					|| noteInfo.forceState == ForceState.AUTO_HOPO;
				noteOutput.Add(new NoteInfo(startTime, endTime - startTime, noteInfo.fret, hopo));
			}

			return noteOutput;
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

		private void ParseBeats(List<EventIR> eventIR, TrackChunk trackChunk) {
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
		}
	}
}