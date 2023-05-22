using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private void ParseVenue(List<EventIR> eventIR, TrackChunk trackChunk) {
			long totalDelta = 0;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is NoteOnEvent noteOnEvent) {
					if (noteOnEvent.NoteNumber != 48) {
						continue;
					}

					eventIR.Add(new EventIR {
						startTick = totalDelta,
						name = "venue_lightFrame_next"
					});
				}

				// Sometimes events are stored as normal text events :/
				if (trackEvent is not BaseTextEvent textEvent) {
					continue;
				}

				// Only look for lighting (for now)
				if (!textEvent.Text.StartsWith("[lighting (") || !textEvent.Text.EndsWith(")]")) {
					continue;
				}

				var argument = textEvent.Text[11..^2];

				// Connect midi lighting name to YARG lighting name
				string eventName = null;
				switch (argument) {
					case "":
						eventName = "venue_light_default";
						break;
					case "strobe_fast":
						eventName = "venue_light_strobeFast";
						break;
					case "stomp":
						eventName = "venue_light_onOffMode";
						break;
				}

				if (eventName == null) {
					continue;
				}

				eventIR.Add(new EventIR {
					startTick = totalDelta,
					name = eventName
				});
			}
		}
	}
}