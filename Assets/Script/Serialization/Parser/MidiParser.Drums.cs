using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private enum CymbalState {
			NONE,
			NO_YELLOW,
			NO_BLUE,
			NO_GREEN
		}

		private struct CymbalStateIR {
			public float start;
			public float end;

			public CymbalState cymbalState;
		}

		private List<NoteInfo> ParseDrums(TrackChunk trackChunk, int difficulty) {
			var tempoMap = midi.GetTempoMap();

			var cymbalStateIR = DrumCymbalStatePass(trackChunk, tempoMap);

			var notes = DrumNotePass(trackChunk, difficulty, tempoMap);
			DrumNoteStatePass(notes, cymbalStateIR);

			return notes;
		}

		private List<CymbalStateIR> DrumCymbalStatePass(TrackChunk trackChunk, TempoMap tempoMap) {
			long totalDelta = 0;

			var cymbalIR = new List<CymbalStateIR>();

			// Since each state has an ON and OFF event,
			// we must store the ON events and wait until the
			// OFF event to actually add the state. This stores
			// the ON event timings.
			long?[] forceStateArray = new long?[3];

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is NoteEvent noteEvent) {
					// Note based flags

					// Look for correct octave
					if (noteEvent.GetNoteOctave() != 8) {
						continue;
					}

					// Convert note to cymbal state (or special)
					CymbalState cymbalState = noteEvent.GetNoteName() switch {
						// Green tom
						NoteName.E => CymbalState.NO_GREEN,
						// Blue tom
						NoteName.DSharp => CymbalState.NO_BLUE,
						// Yellow tom
						NoteName.D => CymbalState.NO_YELLOW,
						// Default
						_ => CymbalState.NONE
					};

					// Skip if not an actual state
					if (cymbalState == CymbalState.NONE) {
						continue;
					}

					// Deal with notes
					int i = (int) cymbalState - 1;
					if (noteEvent is NoteOnEvent) {
						// If it is a note on, wait until we get the note
						// off so we can get the length of the note.
						forceStateArray[i] = totalDelta;
					} else if (noteEvent is NoteOffEvent) {
						if (forceStateArray[i] == null) {
							continue;
						}

						cymbalIR.Add(new CymbalStateIR {
							start = (float) TimeConverter.ConvertTo<MetricTimeSpan>(forceStateArray[i].Value, tempoMap).TotalSeconds,
							end = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds,
							cymbalState = cymbalState
						});

						forceStateArray[i] = null;
					}
				}
			}

			return cymbalIR;
		}

		private List<NoteInfo> DrumNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap) {
			long totalDelta = 0;

			var noteOutput = new List<NoteInfo>();

			// Expert+ is just Expert with double-kick
			bool doubleKick = false;
			if (difficulty == (int) Difficulty.EXPERT_PLUS) {
				doubleKick = true;
				difficulty--;
			}

			// Convert track events into note info
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				var noteName = noteEvent.GetNoteName();
				if (noteEvent.GetNoteOctave() != 4 + difficulty) {
					if (doubleKick && noteEvent.GetNoteOctave() == 6 && noteName == NoteName.B) {
						// Set as kick if double-kick
						noteName = NoteName.C;
					} else {
						continue;
					}
				}

				// Convert note to drum number
				int drum = noteName switch {
					// Orange (Kick)
					NoteName.C => 4,
					// Red
					NoteName.CSharp => 0,
					// Yellow
					NoteName.D => 1,
					// Blue
					NoteName.DSharp => 2,
					// Green
					NoteName.E => 3,
					// Default
					_ => -1
				};

				// Skip if not an actual note
				if (drum == -1) {
					continue;
				}

				// Check if cymbal
				bool isCymbal = drum switch {
					1 or 2 or 3 => true,
					_ => false
				};

				// Deal with notes
				if (noteEvent is NoteOnEvent) {
					// Get start time (in seconds)
					float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

					// Add note
					noteOutput.Add(new NoteInfo {
						time = startTime,
						length = 0f,
						fret = drum,
						hopo = isCymbal
					});
				}
			}

			return noteOutput;
		}

		private void DrumNoteStatePass(List<NoteInfo> noteIR, List<CymbalStateIR> cymbalStateIR) {
			foreach (var note in noteIR) {
				// Only the toms
				if (note.fret != 1 && note.fret != 2 && note.fret != 3) {
					continue;
				}

				// See if we are in any tom force ranges
				bool isTom = false;
				foreach (var cymbalIR in cymbalStateIR) {
					if (cymbalIR.cymbalState != (CymbalState) note.fret) {
						continue;
					}

					if (note.time >= cymbalIR.start && note.time < cymbalIR.end) {
						isTom = true;
						break;
					}
				}

				// Set as tom
				note.hopo = !isTom;
			}
		}
	}
}