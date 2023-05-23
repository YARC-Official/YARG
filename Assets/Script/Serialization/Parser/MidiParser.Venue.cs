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
					case "verse":
						eventName = "venue_light_verse";
						break;
					case "chorus":
						eventName = "venue_light_chorus";
						break;
					case "manual_cool":
						eventName = "venue_light_manual_cool";
						break;
					case "manual_warm":
						eventName = "venue_light_manual_warm";
						break;
					case "dischord":
						eventName = "venue_light_dischord";
						break;
					case "loop_cool":
						eventName = "venue_light_loop_cool";
						break;
					case "silhouettes":
						eventName = "venue_light_silhouettes";
						break;
					case "loop_warm":
						eventName = "venue_light_loop_warm";
						break;
					case "silhouettes_spot":
						eventName = "venue_light_silhouettes_spot";
						break;
					case "frenzy":
						eventName = "venue_light_frenzy";
						break;
					case "blackout_fast":
						eventName = "venue_light_blackout_fast";
						break;
					case "flare_fast":
						eventName = "venue_light_flare_fast";
						break;
					case "searchlights":
						eventName = "venue_light_searchlights";
						break;
					case "flare_slow":
						eventName = "venue_light_flare_slow";
						break;
					case "harmony":
						eventName = "venue_light_harmony";
						break;
					case "sweep":
						eventName = "venue_light_sweep";
						break;
					case "bre":
						eventName = "venue_light_bre";
						break;
					case "strobe_slow":
						eventName = "venue_light_strobe_slow";
						break;
					case "blackout_slow":
						eventName = "venue_light_blackout_slow";
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