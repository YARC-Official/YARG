using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;
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

			var drumType = GetDrumType(trackChunk);
			if (drumType == SongInfo.DrumType.FOUR_LANE) {
				var cymbalStateIR = DrumCymbalStatePass(trackChunk, tempoMap);

				var notes = DrumNotePass(trackChunk, difficulty, tempoMap);
				DrumNoteStatePass(notes, cymbalStateIR);

				return notes;
			} else {
				return DrumFromFiveLane(trackChunk, difficulty, tempoMap);
			}
		}

		private SongInfo.DrumType GetDrumType(TrackChunk trackChunk) {
			if (songInfo.drumType != SongInfo.DrumType.UNKNOWN) {
				return songInfo.drumType;
			}

			// If we don't know the drum type...
			foreach (var midiEvent in trackChunk.Events) {
				if (midiEvent is not NoteEvent note) {
					continue;
				}

				// Look for the expert 5th-lane note
				if (note.NoteNumber == 101) {
					return SongInfo.DrumType.FIVE_LANE;
				}
			}

			// If we didn't find the note, assume 4-lane
			return SongInfo.DrumType.FIVE_LANE;
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

				if (trackEvent is not NoteOnEvent noteEvent) {
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

		private List<NoteInfo> DrumFromFiveLane(TrackChunk trackChunk, int difficulty, TempoMap tempoMap) {
			long totalDelta = 0;

			var noteOutput = new List<NoteInfo>();

			// Expert+ is just Expert with double-kick
			bool doubleKick = false;
			if (difficulty == (int) Difficulty.EXPERT_PLUS) {
				doubleKick = true;
				difficulty--;
			}

			NoteInfo lastNote = null;

			// Convert track events into note info
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteOnEvent noteEvent) {
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
					// Kick
					NoteName.C => 5,
					// Red
					NoteName.CSharp => 0,
					// Yellow
					NoteName.D => 1,
					// Blue
					NoteName.DSharp => 2,
					// Orange
					NoteName.E => 3,
					// Green
					NoteName.F => 4,
					// Default
					_ => -1
				};

				// Skip if not an actual note
				if (drum == -1) {
					continue;
				}

				// Convert to 4-lane
				bool isCymbal = false;
				switch (drum) {
					case 1: // Red -> Red Cymbal
						drum = 1;
						isCymbal = true;
						break;
					case 3: // Orange -> Green Cymbal
						drum = 3;
						isCymbal = true;
						break;
					case 4: // Green -> Green Tom
						drum = 3;
						isCymbal = false;
						break;
					case 5: // Kick
						drum = 4;
						break;
				}

				// Get start time (in seconds)
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

				// Check for Green Cymbal + Green Tom collision
				if (lastNote != null && lastNote.time == startTime) {
					if (lastNote.fret == 3 && lastNote.hopo &&
						drum == 3 && !isCymbal) {

						drum = 2;
						isCymbal = false;
					} else if (lastNote.fret == 3 && !lastNote.hopo &&
						drum == 3 && isCymbal) {

						lastNote.fret = 2;
						lastNote.hopo = false;
					}
				}

				// Add note
				lastNote = new NoteInfo {
					time = startTime,
					length = 0f,
					fret = drum,
					hopo = isCymbal
				};
				noteOutput.Add(lastNote);
			}

			return noteOutput;
		}
	}
}