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
			OPEN
		}

		private class FiveFretIR {
			public long startTick;
			// This is an array due to extended sustains
			public long[] endTick;

			public FretFlag fretFlag;
			public bool hopo;

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

			var noteIR = FiveFretNotePass(trackChunk, difficulty);
			FiveFretNoteStatePass(noteIR, forceStateIR, tempoMap);

			var noteOutput = FiveFretIrToRealPass(noteIR, tempoMap);
			return noteOutput;
		}

		private List<ForceStateIR> FiveFretGetForceState(TrackChunk trackChunk, int difficulty) {
			long totalDelta = 0;

			var forceIR = new List<ForceStateIR>();

			// Since each state has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the state. This stores
			// the ON event timings.
			long?[] forceStateArray = new long?[4];

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
					if (header.SequenceEqual(SYSEX_OPEN_NOTE)) {
						i = 2;
						forceState = ForceState.OPEN;
					} else if (header.SequenceEqual(SYSEX_TAP_NOTE)) {
						i = 3;
						forceState = ForceState.HOPO;
					} else {
						continue;
					}

					if (sysExEvent.Data[6] == 0x01) {
						// If it is a flag on, wait until we get the flag
						// off so we can get the length of the flag period.
						forceStateArray[i] = totalDelta;
					} else {
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
				} else if (trackEvent is NoteEvent noteEvent) {
					// Note based flags

					// Look for correct octave
					if (noteEvent.GetNoteOctave() != 4 + difficulty) {
						continue;
					}

					// Convert note to force state
					ForceState forceState = noteEvent.GetNoteName() switch {
						// Force HOPO
						NoteName.F => ForceState.HOPO,
						// Force strum
						NoteName.FSharp => ForceState.STRUM,
						// Default
						_ => ForceState.NONE
					};

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

		private List<FiveFretIR> FiveFretNotePass(TrackChunk trackChunk, int difficulty) {
			long totalDelta = 0;

			var noteIR = new List<FiveFretIR>();
			var currentChord = new FiveFretIR();

			// Since each note has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the note. This stores
			// the ON event timings.
			long?[] fretState = new long?[5];

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
				int fret = noteEvent.GetNoteName() switch {
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
					FretFlag fretFlag = fret switch {
						0 => FretFlag.GREEN,
						1 => FretFlag.RED,
						2 => FretFlag.YELLOW,
						3 => FretFlag.BLUE,
						4 => FretFlag.ORANGE,
						_ => FretFlag.NONE
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
							endTick = new long[5],
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
				ForceState force = ForceState.NONE;
				foreach (var forceIR in forceStateIR) {
					if (note.startTick >= forceIR.startTick && note.startTick < forceIR.endTick) {
						force = forceIR.forceState;
						break;
					}
				}

				if (force == ForceState.NONE) {
					// If there is not any force state, we know that we need to look for auto-HOPO.
					if (DoesQualifyForAutoHopo(note.fretFlag, lastFret)) {
						// Wrap in a try just in case noteBeat < lastNoteBeat
						try {
							var noteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(note.startTick, tempo);
							var lastNoteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(lastTime, tempo);
							var distance = noteBeat - lastNoteBeat;

							// Use HOPO frequency value from song info.
							// Convert the ticks to a musical time span.
							if (distance <= new MusicalTimeSpan(songInfo.hopoFreq, 480 * 4)) {
								note.hopo = true;
								note.autoHopo = true;
							}
						} catch { }
					}
				} else if (force == ForceState.OPEN) {
					// Set as open if requested
					note.fretFlag = FretFlag.OPEN;
					note.hopo = false;
				} else {
					// Otherwise, just set as a HOPO if requested
					note.hopo = force == ForceState.HOPO;
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

					// Get the end tick (different for open notes)
					long endTick;
					if (fret == 5) {
						endTick = noteInfo.startTick + 1;
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