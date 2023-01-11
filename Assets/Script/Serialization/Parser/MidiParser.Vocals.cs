using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using YARG.Data;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private List<GenericLyricInfo> ParseGenericLyrics(TrackChunk trackChunk, TempoMap tempo) {
			var lyrics = new List<GenericLyricInfo>();

			long totalDelta = 0;

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

			return lyrics;
		}
	}
}