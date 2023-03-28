using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private List<NoteInfo> ParseGHDrums(TrackChunk trackChunk, int difficulty) {
			var tempoMap = midi.GetTempoMap();

			var drumType = GetDrumType(trackChunk);
			if (drumType == SongInfo.DrumType.FOUR_LANE) {
				// TODO
				return null;
			} else {
				return GHDrumNotePass(trackChunk, difficulty, tempoMap);
			}
		}

		private List<NoteInfo> GHDrumNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap) {
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

				// Get start time (in seconds)
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

				// Add note
				noteOutput.Add(new NoteInfo {
					time = startTime,
					length = 0f,
					fret = drum
				});
			}

			return noteOutput;
		}
	}
}