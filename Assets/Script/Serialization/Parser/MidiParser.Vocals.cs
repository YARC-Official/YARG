using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private List<GenericLyricInfo> ParseGenericLyrics(TrackChunk trackChunk, TempoMap tempo) {
			var lyrics = new List<GenericLyricInfo>();

			// For later:
			// = is real dash
			// # is inharmonic
			// / is split phrase?
			// + is unknown

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

					string l = lyricEvent.Text;

					// Remove state changes
					if (l.StartsWith("[") && l.EndsWith("]")) {
						continue;
					}

					// Remove special case
					if (l == "+") {
						continue;
					}

					// Remove inharmonic tag and slash
					if (l.EndsWith("#") || l.EndsWith("/")) {
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

		/*
		// Convert track events into intermediate representation
			GenericLyricInfo currentGroup = null;
			bool connected = false;
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;
				
				if (trackEvent is not BaseTextEvent lyricEvent) {
					continue;
				}

				// Skip if track name
				if (lyricEvent.Text == "PART VOCALS") {
					continue;
				}

				// Combine lyric events together into phrases
				
				var time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo).TotalSeconds;
				string text = lyricEvent.Text;

				// Skip if state stats
				if (text.StartsWith('[') && text.EndsWith(']')) {
					continue;
				}

				// Remove all metadata
				for (int i = text.Length - 1; i >= 0; i--) {
					if (text[i] == '#' || (
						!char.IsWhiteSpace(text[i]) &&
						!char.IsLetter(text[i]) &&
						!char.IsNumber(text[i]) &&
						text[i] != '\'' &&
						text[i] != '-')) {
						
						text = text.Remove(i, 1);
					}
				}
				text = text.Trim();

				// Skip blanks
				if (string.IsNullOrEmpty(text)) {
					continue;
				}
				
				// Uppercase is a new phrase
				if (currentGroup != null && char.IsUpper(text[0])) {
					lyrics.Add(currentGroup);
					connected = false;
					currentGroup = null;
				}

				if (currentGroup == null) {
					currentGroup = new GenericLyricInfo(time, lyricEvent.Text);
				} else {
					if (!connected) {
						currentGroup.lyric += " ";
					}

					if (text.EndsWith("-")) {
						connected = true;
						currentGroup.lyric += text[..^1];
					} else {
						connected = false;
						currentGroup.lyric += text;
					}
				}
			}
			
			// Deal with last lyric group
			if (currentGroup != null) {
				lyrics.Add(currentGroup);
			}
			*/
	}
}