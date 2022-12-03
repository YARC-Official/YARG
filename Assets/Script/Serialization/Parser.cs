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
						chart = ParseGuitar(trackChunk, midi.GetTempoMap());
					}

					if (trackName.Text == "BEAT") {
						chartEvents = ParseBeats(trackChunk, midi.GetTempoMap());
					}
				}
			}

			// Sort by time
			chart?.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
			chartEvents?.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
		}

		private static List<NoteInfo> ParseGuitar(TrackChunk trackChunk, TempoMap tempo) {
			var enumerable = new List<TrackChunk>(1) { trackChunk };
			var rawNotes = enumerable.GetNotes();

			var notes = new List<NoteInfo>(rawNotes.Count);
			foreach (var rawNote in rawNotes) {
				// Expert octave
				if (rawNote.Octave != 7) {
					continue;
				}

				// Convert note to fret number
				int fretNum = rawNote.NoteName switch {
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

				// Get timing info
				float time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(rawNote.Time, tempo).TotalSeconds;
				float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(rawNote.EndTime, tempo).TotalSeconds;

				// Add the note
				notes.Add(new NoteInfo(time, fretNum, endTime - time));
			}

			return notes;
		}

		private static List<EventInfo> ParseBeats(TrackChunk trackChunk, TempoMap tempo) {
			var enumerable = new List<TrackChunk>(1) { trackChunk };
			var rawNotes = enumerable.GetNotes();

			var events = new List<EventInfo>(rawNotes.Count);
			foreach (var rawNote in rawNotes) {
				// Convert note to beat line type
				int majorOrMinor = rawNote.NoteName switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					_ => -1
				};

				// Skip if not a beat line
				if (majorOrMinor == -1) {
					continue;
				}

				// Get timing info
				float time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(rawNote.Time, tempo).TotalSeconds;

				// Add to track
				if (majorOrMinor == 1) {
					events.Add(new EventInfo(time, "beatLine_minor"));
				} else {
					events.Add(new EventInfo(time, "beatLine_major"));
				}
			}

			return events;
		}
	}
}