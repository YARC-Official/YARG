using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		//A bunch of the code is duplicated between generic and vocal lyrics. Can probably be made into functions or a base class or something.
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
				var length = (float) TimeConverter.ConvertTo<MetricTimeSpan>(note.EndTime, tempo).TotalSeconds - time;
				GenericLyricInfo lyric = new(time, length, new());

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

					// Get lyric text
					string l = lyricEvent.Text.Trim();
					if (textEventRegex.IsMatch(l)) {
						// This is a normal text event, not a lyric
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

					// Remove ignored tags (for now)
					if (l.EndsWith("/") || l.EndsWith("%")) {
						l = l[..^1];
					}

					// Replace
					l = l.Replace('=', '-');
					l = l.Replace('_', ' ');
					l = l.Replace('§', '‿');

					// Add to phrase
					var lyricTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo).TotalSeconds;
					lyric.lyric.Add((lyricTime, l));
				}

				// Add phrase to result
				// Ignore phrases w/o lyrics (i.e. percussion)
				if (lyric.lyric.Count > 0) {
					lyrics.Add(lyric);
				}
			}

			return lyrics;
		}

		private List<LyricInfo> ParseRealLyrics(List<EventIR> eventIR, TrackChunk trackChunk, TempoMap tempo, int harmonyIndex) {
			return ParseRealLyrics(eventIR, trackChunk, trackChunk, tempo, harmonyIndex);
		}

		private List<LyricInfo> ParseRealLyrics(List<EventIR> eventIR, TrackChunk trackChunk, TrackChunk phraseTimingTrack, TempoMap tempo, int harmonyIndex) {
			// Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Vocals.md

			var lyrics = new List<LyricInfo>();

			// Get lyric phrase timings
			HashSet<long> startTimings = new();
			foreach (var note in phraseTimingTrack.GetNotes()) {
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

					// Get lyric text
					string l = lyricEvent.Text.Trim();
					if (textEventRegex.IsMatch(l)) {
						// This is a normal text event, not a lyric
						continue;
					}

					bool inharmonic = false;

					// Get lyric information
					var lyricInfo = GetLyricInfo(trackChunk, totalDelta);
					long endTime = totalDelta + 80;
					NoteName noteName = NoteName.C;
					int octave = 3;
					if (!lyricInfo.HasValue) {
						inharmonic = true;
					} else {
						(endTime, noteName, octave) = lyricInfo.Value;
					}

					// Convert to seconds
					var lyricTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo).TotalSeconds;
					var lyricEnd = (float) TimeConverter.ConvertTo<MetricTimeSpan>(endTime, tempo).TotalSeconds;

					// Check for hidden marker
					bool hidden = false;
					if (l.EndsWith("$")) {
						hidden = true;
						l = l[..^1];
					}

					// Extend last lyric if +
					if (l == "+" || l == "+-") {
						var lyric = lyrics[^1];

						// Workaround for Guitar Hero weirdness
						if (l == "+-") {
							lyric.lyric += "-";
						}

						// Add end pointer for first note
						var (_, (firstNote, firstOctave)) = lyric.pitchOverTime[^1];
						lyric.pitchOverTime.Add((lyric.length, (firstNote, firstOctave)));

						// Update length
						lyric.length = lyricEnd - lyric.time;

						// Add start pointer for next note
						lyric.pitchOverTime.Add((lyricTime - lyric.time, ((float) noteName, octave)));

						continue;
					}

					// Set inharmonic
					if (l.EndsWith("#") || l.EndsWith("^") || l.EndsWith("*")) {
						inharmonic = true;
						l = l[..^1];
					}

					// Remove ignored tags (for now)
					if (l.EndsWith("/") || l.EndsWith("%")) {
						l = l[..^1];
					}

					// Replace
					l = l.Replace('=', '-');
					l = l.Replace('_', ' ');
					l = l.Replace('§', '‿');

					// Replace lyric with nothing if hidden
					if (harmonyIndex != 0 && hidden) {
						l = "";
					}

					// Add to lyrics
					lyrics.Add(new LyricInfo {
						time = lyricTime,
						length = lyricEnd - lyricTime,
						lyric = l,
						inharmonic = inharmonic,
						pitchOverTime = new() {
							(0f, ((float) noteName, octave))
						}
					});
				}

				// Add end phrase event
				if (harmonyIndex == 0) {
					eventIR.Add(new EventIR {
						startTick = note.EndTime,
						name = "harmVocal_endPhrase"
					});
				} else if (harmonyIndex == -1) {
					eventIR.Add(new EventIR {
						startTick = note.EndTime,
						name = "vocal_endPhrase"
					});
				}
			}

			return lyrics;
		}

		private (long endTime, NoteName note, int octave)? GetLyricInfo(TrackChunk trackChunk, long tick) {
			foreach (var note in trackChunk.GetNotes()) {
				// Skip meta-data notes
				if (note.Octave >= 7) {
					continue;
				}

				// Look for the note in question
				if (note.Time != tick) {
					continue;
				}

				return (note.EndTime, note.NoteName, note.Octave);
			}

			return null;
		}
	}
}
