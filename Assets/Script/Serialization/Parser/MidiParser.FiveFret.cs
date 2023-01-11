using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		[Flags]
		private enum FretFlag {
			NONE = 0,
			GREEN = 1,
			RED = 2,
			YELLOW = 4,
			BLUE = 8,
			ORANGE = 16
		}

		private enum ForceState {
			NONE,
			HOPO,
			STRUM
		}

		private class FiveFretIR {
			public long startTick;
			// This is an array due to extended sustains
			public long[] endTick;

			public FretFlag fretFlag;
			public bool hopo;
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
			long?[] forceStateArray = new long?[2];

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

							// Thanks?? https://tcrf.net/Proto:Guitar_Hero
							// According to this, auto-HOPO threshold is 170 ticks.
							// "But a tick is different in every midi file!"
							// It also mentions that 160 is a twelth note.
							// 160 * 12 = 1920
							if (distance <= new MusicalTimeSpan(170, 1920)) {
								note.hopo = true;
							}
						} catch { }
					}
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

					// Get start time and end time (in seconds)
					float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.startTick, tempoMap).TotalSeconds;
					float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.endTick[fret], tempoMap).TotalSeconds;

					// Add note
					noteOutput.Add(new NoteInfo(startTime, endTime - startTime, fret, noteInfo.hopo));
				}
			}

			return noteOutput;
		}

		private static bool DoesQualifyForAutoHopo(FretFlag flag, FretFlag lastFret) {
			if (flag == lastFret) {
				return false;
			}

			if (!IsFlagSingleNote(flag)) {
				return false;
			}

			return true;
		}

		private static bool IsFlagSingleNote(FretFlag flag) {
			// Check for only 1 bit enabled
			return flag != 0 && (flag & (flag - 1)) == 0;
		}
	}
}