using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private static readonly byte[] SYSEX_OPEN_NOTE = {
			0x50, 0x53, 0x00, 0x00, 0x03, 0x01
		};

		private static readonly byte[] SYSEX_TAP_NOTE = {
			0x50, 0x53, 0x00, 0x00, 0xFF, 0x04
		};

		private enum ForceState {
			NONE,
			HOPO,
			STRUM,
			OPEN,
			TAP
		}

		private bool isCurrentlyTap = false;

		private class FiveFretIR {
			public long startTick;
			// This is an array due to extended sustains
			public long[] endTick;

			public FretFlag fretFlag;
			public FretFlag prevFretFlag;
			public bool hopo;
			public bool tap;

			// Used for difficulty downsampling
			public bool autoHopo;
		}

		private struct ForceStateIR {
			public long startTick;
			public long endTick;

			public ForceState forceState;
		}

		private List<NoteInfo> ParseFiveFret(TrackChunk trackChunk, int difficulty) {
			var tempoMap = midi.GetTempoMap();

			var forceStateIR = FiveFretGetForceState(trackChunk, difficulty);

			bool enhancedOpen = LookForEnhancedOpen(trackChunk);
			var noteIR = FiveFretNotePass(trackChunk, difficulty, enhancedOpen);
			FiveFretNoteStatePass(noteIR, forceStateIR, tempoMap);

			var noteOutput = FiveFretIrToRealPass(noteIR, tempoMap);
			return noteOutput;
		}

		private bool LookForEnhancedOpen(TrackChunk trackChunk) {
			foreach (var trackEvent in trackChunk.Events) {
				if (trackEvent is not BaseTextEvent textEvent) {
					continue;
				}

				if (textEvent.Text == "[ENHANCED_OPENS]") {
					return true;
				}
			}

			return false;
		}

		private List<ForceStateIR> FiveFretGetForceState(TrackChunk trackChunk, int difficulty) {
			long totalDelta = 0;

			var forceIR = new List<ForceStateIR>();

			// Since each state has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the state. This stores
			// the ON event timings.
			long?[] forceStateArray = new long?[5];

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is SysExEvent sysExEvent) {
					// SysEx based flags (WHY????)

					// Skip if not right data length
					if (sysExEvent.Data.Length != 8) {
						continue;
					}

					var header = sysExEvent.Data.SkipLast(2);

					// Look for open note OR tap note header
					int i;
					ForceState forceState;
					var sysExOpen = SYSEX_OPEN_NOTE;
					sysExOpen[4] = (byte)difficulty;
					if (header.SequenceEqual(sysExOpen)) {
						i = 2;
						forceState = ForceState.OPEN;
					} else if (header.SequenceEqual(SYSEX_TAP_NOTE)) {
						i = 3;
						forceState = ForceState.TAP;
					} else {
						continue;
					}

					if (sysExEvent.Data[6] == 0x01) {
						// If it is a flag on, wait until we get the flag
						// off so we can get the length of the flag period.
						forceStateArray[i] = totalDelta;
						if (forceState == ForceState.TAP) {
							isCurrentlyTap = true;
						}
					} else {
						if (forceStateArray[i] == null) {
							continue;
						}
						if (forceState == ForceState.TAP) {
							isCurrentlyTap = false;
						}
						forceIR.Add(new ForceStateIR {
							startTick = forceStateArray[i].Value,
							endTick = totalDelta,
							forceState = forceState
						});

						forceStateArray[i] = null;
					}
				} else if (trackEvent is NoteEvent noteEvent && !isCurrentlyTap) {
					// Note based flags

					// Look for correct octave
					if (noteEvent.GetNoteOctave() != 4 + difficulty) {
						continue;
					}
					ForceState forceState = ForceState.NONE;
					if (noteEvent.GetNoteOctave() == 7 && noteEvent.GetNoteName() == NoteName.GSharp) {
						forceState = ForceState.TAP;
					} else {
						// Convert note to force state
						forceState = noteEvent.GetNoteName() switch {
							// Force HOPO
							NoteName.F => ForceState.HOPO,
							// Force strum
							NoteName.FSharp => ForceState.STRUM,
							// Default
							_ => ForceState.NONE
						};
					}

					// Skip if not an actual state
					if (forceState == ForceState.NONE) {
						continue;
					}

					// Deal with notes
					int i = (int) forceState - 1;
					if (noteEvent is NoteOnEvent) {
						// If it is a note on, wait until we get the note
						// off so we can get the length of the note.
						forceStateArray[i] = totalDelta;
					} else if (noteEvent is NoteOffEvent) {
						if (forceStateArray[i] == null) {
							continue;
						}

						forceIR.Add(new ForceStateIR {
							startTick = forceStateArray[i].Value,
							endTick = totalDelta,
							forceState = forceState
						});

						forceStateArray[i] = null;
					}
				}
			}

			return forceIR;
		}

		private List<FiveFretIR> FiveFretNotePass(TrackChunk trackChunk, int difficulty, bool enhancedOpen) {
			long totalDelta = 0;

			var noteIR = new List<FiveFretIR>();
			var currentChord = new FiveFretIR();

			// Since each note has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the note. This stores
			// the ON event timings.
			long?[] fretState = new long?[6];

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				var fret = -1;
				if (noteEvent.GetNoteOctave() != 4 + difficulty) {
					if (enhancedOpen && noteEvent.GetNoteOctave() == 3 + difficulty &&
					    noteEvent.GetNoteName() == NoteName.B) {

						fret = 5;
					} else {
						continue;
					}
				}

				// Convert note to fret number (or special)
				if (fret == -1) {
					fret = noteEvent.GetNoteName() switch {
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
						// Default
						_ => -1
					};
				}

				// Skip if not an actual note
				if (fret == -1) {
					continue;
				}

				// Deal with notes
				if (noteEvent is NoteOnEvent) {
					// If it is a note on, wait until we get the note
					// off so we can get the length of the note.
					fretState[fret] = totalDelta;
				} else if (noteEvent is NoteOffEvent) {
					var fretFlag = fret switch {
						0 => FretFlag.GREEN,
						1 => FretFlag.RED,
						2 => FretFlag.YELLOW,
						3 => FretFlag.BLUE,
						4 => FretFlag.ORANGE,
						5 => FretFlag.OPEN,
						_ => throw new Exception("Unreachable.")
					};

					// Here is were the notes are actually stored.
					// We now know the starting point and ending point.

					if (fretState[fret] == null) {
						continue;
					}

					// Collect the notes in chords.
					// If the chord is complete, add it.
					if (currentChord.startTick != fretState[fret]) {
						noteIR.Add(currentChord);
						currentChord = new FiveFretIR {
							startTick = fretState[fret].Value,
							endTick = new long[6],
							fretFlag = fretFlag,
							hopo = false
						};

						currentChord.endTick[fret] = totalDelta;
					} else {
						currentChord.endTick[fret] = totalDelta;
						currentChord.fretFlag |= fretFlag;
					}

					// Reset fret state and wait until next ON event
					fretState[fret] = null;
				}
			}

			// Remove the first note IR as it is empty
			noteIR.Add(currentChord);
			noteIR.RemoveAt(0);

			return noteIR;
		}

		private void FiveFretNoteStatePass(List<FiveFretIR> noteIR, List<ForceStateIR> forceStateIR, TempoMap tempo) {
			long lastTime = -1000;
			FretFlag lastFret = FretFlag.NONE;

			foreach (var note in noteIR) {
				// See if we are in any force ranges
				List<ForceState> force = new();
				foreach (var forceIR in forceStateIR) {
					if (note.startTick == forceIR.startTick || (note.startTick >= forceIR.startTick && note.startTick < forceIR.endTick)) {
						force.Add(forceIR.forceState);
					}/* else if (forceIR.endTick >= note.startTick) {
						break;
					}*/
				}

				// Force open note before checking auto-hopo
				note.prevFretFlag = note.fretFlag;
				if (force.Contains(ForceState.OPEN)) {
					note.fretFlag = FretFlag.OPEN;
				}
				if (force.Count == 0 || (force.Count == 1 && force.Contains(ForceState.OPEN))) {
					// If there is not any force state, we know that we need to look for auto-HOPO. (This include open notes)
					if (DoesQualifyForAutoHopo(note.fretFlag, lastFret)) {
						// Wrap in a try just in case noteBeat < lastNoteBeat
						try {
							var noteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(note.startTick, tempo);
							var lastNoteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(lastTime, tempo);
							var distance = noteBeat - lastNoteBeat;

							// Use HOPO frequency value from song info.
							// Convert the ticks to a musical time span.
							if (distance <= new MusicalTimeSpan(songEntry.HopoThreshold, 480 * 4)) {
								note.hopo = true;
								note.autoHopo = true;
							}
						} catch { }
					}
				} else {
					// Otherwise, just set as a HOPO if requested
					note.hopo = force.Contains(ForceState.HOPO);
				}
				if (force.Contains(ForceState.TAP)) {
					note.tap = true;
					note.hopo = false;
					note.autoHopo = false;
				}
				lastTime = note.startTick;
				lastFret = note.fretFlag;
			}
		}

		private List<NoteInfo> FiveFretIrToRealPass(List<FiveFretIR> noteIR, TempoMap tempoMap) {
			var noteOutput = new List<NoteInfo>(noteIR.Count);

			// Return empty list if no notes are available in this difficulty
			if (noteIR.Count <= 0) {
				return noteOutput;
			}

			// IR into real
			foreach (var noteInfo in noteIR) {
				var enums = (FretFlag[]) Enum.GetValues(typeof(FretFlag));
				for (int i = 1; i < enums.Length; i++) {
					var flag = enums[i];

					// Go through each flag
					if (!noteInfo.fretFlag.HasFlag(flag)) {
						continue;
					}

					int fret = i - 1;

					if (noteInfo.tap) {
						noteInfo.hopo = false;
						noteInfo.autoHopo = false;
					}

					// Get the end tick (different for open notes)
					long endTick = noteInfo.startTick + 1;
					if (fret == 5) {
						// If it's an open note, it has to grab it's sustain from whatever fret it was before the marker was applied
						for (int j = 1; j < enums.Length; j++) {
							if (!noteInfo.prevFretFlag.HasFlag(enums[j])) {
								continue;
							}
							endTick = noteInfo.endTick[j - 1];
							break;
						}
					} else {
						endTick = noteInfo.endTick[fret];
					}

					// Get start time and end time (in seconds)
					float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.startTick, tempoMap).TotalSeconds;
					float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(endTick, tempoMap).TotalSeconds;

					// Add note
					noteOutput.Add(new NoteInfo {
						time = startTime,
						length = endTime - startTime,
						fret = fret,
						tap = noteInfo.tap,
						hopo = noteInfo.hopo,
						autoHopo = noteInfo.autoHopo
					});
				}
			}

			return noteOutput;
		}

		private static bool DoesQualifyForAutoHopo(FretFlag flag, FretFlag lastFret) {
			if (flag == lastFret) {
				return false;
			}

			if (!flag.IsFlagSingleNote()) {
				return false;
			}

			return true;
		}
	}
}