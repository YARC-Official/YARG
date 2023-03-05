using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private List<GenericLyricInfo> ParseGenericLyrics(TrackChunk trackChunk, TempoMap tempo) {
			var lyrics = new List<GenericLyricInfo>();

			// Get lyric phrase timings
			HashSet<long> startTimings = new();
			foreach (var note in trackChunk.GetNotes()) {
				// B7 note indicates phrases
				if (note.Octave != 7) {
					continue;
				}
				if (note.NoteName != NoteName.A && note.NoteName != NoteName.ASharp) {
					continue;
				}

				// Skip if there is already a phrase with the same start time
				if (startTimings.Contains(note.Time)) {
					continue;
				}

				// Create lyric
				startTimings.Add(note.Time);
				var time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempo).TotalSeconds;
				GenericLyricInfo lyric = new(time, new());

				// Get lyrics from this phrase
				long totalDelta = 0;
				foreach (var trackEvent in trackChunk.Events) {
					totalDelta += trackEvent.DeltaTime;

					// Sometimes lyrics are stored as normal text events :/
					if (trackEvent is not BaseTextEvent lyricEvent) {
						continue;
					}

					// Skip all lyrics until we are in range
					if (totalDelta < note.Time) {
						continue;
					}

					// If we encounter a lyric outside of range, we are done
					if (totalDelta >= note.EndTime) {
						break;
					}

					string l = lyricEvent.Text.Trim();

					// Remove state changes
					if (l.StartsWith("[") && l.EndsWith("]")) {
						continue;
					}

					// Remove special case
					if (l == "+") {
						continue;
					}

					// Remove inharmonic tag and slash
					if (l.EndsWith("#") || l.EndsWith("/") || l.EndsWith("^")) {
						l = l[0..^1];
					}

					// Add to phrase
					var lyricTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo).TotalSeconds;
					lyric.lyric.Add((lyricTime, l));
				}

				// Add phrase to result
				lyrics.Add(lyric);
			}

			return lyrics;
		}

		private List<LyricInfo> ParseRealLyrics(List<EventIR> eventIR, TrackChunk trackChunk, TempoMap tempo) {
			var lyrics = new List<LyricInfo>();

			// For later:
			// = is real dash
			// # is inharmonic
			// / is split phrase?
			// + is connect two notes
			// ^ is unknown

			// Get lyric phrase timings
			HashSet<long> startTimings = new();
			foreach (var note in trackChunk.GetNotes()) {
				// B7 note indicates phrases
				if (note.Octave != 7) {
					continue;
				}
				if (note.NoteName != NoteName.A && note.NoteName != NoteName.ASharp) {
					continue;
				}

				// Skip if there is already a phrase with the same start time
				if (startTimings.Contains(note.Time)) {
					continue;
				}

				// Add phrase
				startTimings.Add(note.Time);

				// Get lyrics from this phrase
				long totalDelta = 0;
				foreach (var trackEvent in trackChunk.Events) {
					totalDelta += trackEvent.DeltaTime;

					// Sometimes lyrics are stored as normal text events :/
					if (trackEvent is not BaseTextEvent lyricEvent) {
						continue;
					}

					// Skip all lyrics until we are in range
					if (totalDelta < note.Time) {
						continue;
					}

					// If we encounter a lyric outside of range, we are done
					if (totalDelta >= note.EndTime) {
						break;
					}

					string l = lyricEvent.Text.Trim();

					// Remove state changes
					if (l.StartsWith("[") && l.EndsWith("]")) {
						continue;
					}

					// Get end time
					long endTime = GetLyricEndTime(trackChunk, totalDelta);

					// Convert to seconds
					var lyricTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo).TotalSeconds;
					var lyricEnd = (float) TimeConverter.ConvertTo<MetricTimeSpan>(endTime, tempo).TotalSeconds;

					// Extend last lyric if +
					if (l == "+") {
						var lyric = lyrics[^1];
						lyric.length = lyricEnd - lyric.time;

						continue;
					}

					bool inharmonic = true;

					// Set inharmonic
					if (l.EndsWith("#")) {
						inharmonic = true;
						l = l[0..^1];
					}

					// Remove other tags
					if (l.EndsWith("/") || l.EndsWith("^")) {
						l = l[0..^1];
					}

					// Replace = with -
					l = l.Replace('=', '-');

					// Add to lyrics
					lyrics.Add(new LyricInfo(lyricTime, lyricEnd - lyricTime, l, inharmonic));
				}

				// Add end phrase event
				eventIR.Add(new EventIR {
					startTick = note.EndTime,
					name = "vocal_endPhrase"
				});
			}

			return lyrics;
		}

		private long GetLyricEndTime(TrackChunk trackChunk, long tick) {
			foreach (var note in trackChunk.GetNotes()) {
				// Skip meta-data notes
				if (note.Octave >= 7) {
					continue;
				}

				// Look for the note in question
				if (note.Time != tick) {
					continue;
				}

				return note.EndTime;
			}

			return tick;
		}
	}
}