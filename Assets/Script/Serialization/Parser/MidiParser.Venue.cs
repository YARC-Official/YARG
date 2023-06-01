using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private void ParseVenue(List<EventIR> eventIR, TrackChunk trackChunk) {
			long totalDelta = 0;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				// Track the lighting keyframe but in the new style
				if(trackEvent is TextEvent textEv){
					if(textEv.Text == "[next]"){
						eventIR.Add(new EventIR {
							startTick = totalDelta,
							name = "venue_lightFrame_next"
						});
					}
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
				string eventName = argument switch {
					"" => "venue_light_default",
					"strobe_fast" => "venue_light_strobeFast",
					"verse" => "venue_light_verse",
					"chorus" => "venue_light_chorus",
					"manual_cool" => "venue_light_manualCool",
					"manual_warm" => "venue_light_manualWarm",
					"dischord" => "venue_light_dischord",
					"loop_cool" => "venue_light_loopCool",
					"silhouettes" => "venue_light_silhouettes",
					"loop_warm" => "venue_light_loopWarm",
					"silhouettes_spot" => "venue_light_silhouettesSpot",
					"frenzy" => "venue_light_frenzy",
					"blackout_fast" => "venue_light_blackoutFast",
					"flare_fast" => "venue_light_flareFast",
					"searchlights" => "venue_light_searchlights",
					"flare_slow" => "venue_light_flareSlow",
					"harmony" => "venue_light_harmony",
					"sweep" => "venue_light_sweep",
					"bre" => "venue_light_bre",
					"strobe_slow" => "venue_light_strobeSlow",
					"blackout_slow" => "venue_light_blackoutSlow",
					"stomp" => "venue_light_stomp",
					_ => null
				};

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