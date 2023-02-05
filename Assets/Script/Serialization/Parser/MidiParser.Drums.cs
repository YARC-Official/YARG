using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private List<NoteInfo> ParseDrums(TrackChunk trackChunk, int difficulty) {
			var tempoMap = midi.GetTempoMap();
			return DrumNotePass(trackChunk, difficulty, tempoMap);
		}

		private List<NoteInfo> DrumNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap) {
			long totalDelta = 0;

			var noteOutput = new List<NoteInfo>();

			// Convert track events into note info
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != 4 + difficulty) {
					continue;
				}

				// Convert note to drum number
				int drum = noteEvent.GetNoteName() switch {
					// Orange (Kick)
					NoteName.C => 4,
					// Green
					NoteName.CSharp => 0,
					// Red
					NoteName.D => 1,
					// Yellow
					NoteName.DSharp => 2,
					// Blue
					NoteName.E => 3,
					// Default
					_ => -1
				};

				// Skip if not an actual note
				if (drum == -1) {
					continue;
				}

				// Deal with notes
				if (noteEvent is NoteOnEvent) {
					// Get start time (in seconds)
					float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

					// Add note
					noteOutput.Add(new NoteInfo {
						time = startTime,
						length = 0f,
						fret = drum
					});
				}
			}

			return noteOutput;
		}
	}
}