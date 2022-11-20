using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace YARG.Serialization {
	public static class Parser {
		public static void Parse(string midiFile, out List<NoteInfo> chart, out List<EventInfo> chartEvents) {
			var midi = MidiFile.Read(midiFile);

			chart = null;
			chartEvents = null;
			foreach (var trackChunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in trackChunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					if (trackName.Text == "PART GUITAR") {
						chart = ParseGuitar(trackChunk.Events, midi.GetTempoMap());
					}

					if (trackName.Text == "BEAT") {
						chartEvents = ParseBeats(trackChunk.Events, midi.GetTempoMap());
					}
				}
			}

			// Sort by time
			chart?.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
			chartEvents?.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
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

		private static List<EventInfo> ParseBeats(EventsCollection trackEvents, TempoMap tempo) {
			List<EventInfo> output = new(trackEvents.Count);

			long totalDelta = 0;
			foreach (var trackEvent in trackEvents) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is NoteOnEvent noteOnEvent) {
					// Beat octave
					if (noteOnEvent.GetNoteOctave() != 0) {
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

					// Convert delta to real time
					var metricTime = TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo);
					float time = (float) metricTime.TotalSeconds;

					// Add to track
					output.Add(new EventInfo(time, "beatLine"));
				}
			}

			return output;
		}
	}
}