using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace YARG {
	public static class Parser {
		public static List<NoteInfo> Parse(string midiFile) {
			var midi = MidiFile.Read(midiFile);

			List<NoteInfo> output = null;
			foreach (var trackChunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in trackChunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					if (trackName.Text != "PART GUITAR") {
						break;
					}

					output = ParseGuitar(trackChunk.Events, midi.GetTempoMap());
					break;
				}
			}

			// Sort by time
			output.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));

			return output;
		}

		private static List<NoteInfo> ParseGuitar(EventsCollection trackEvents, TempoMap tempo) {
			List<NoteInfo> output = new(trackEvents.Count);

			long totalDelta = 0;
			foreach (var trackEvent in trackEvents) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is NoteOnEvent noteOnEvent) {
					// Expert octave
					if (noteOnEvent.GetNoteOctave() != 7) {
						continue;
					}

					// Convert note to fret number
					int fretNum = noteOnEvent.GetNoteName() switch {
						NoteName.C => 0,
						NoteName.CSharp => 1,
						NoteName.D => 2,
						NoteName.DSharp => 3,
						NoteName.E => 4,
						_ => -1
					};

					// Skip if not an actual note
					if (fretNum == -1) {
						continue;
					}

					// Convert delta to real time
					var metricTime = TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo);
					float time = (float) metricTime.TotalSeconds;

					// Add to track
					output.Add(new NoteInfo(time, fretNum));
				}
			}

			return output;
		}
	}
}