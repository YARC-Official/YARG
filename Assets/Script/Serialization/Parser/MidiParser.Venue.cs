using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Core;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		// Matches lighting events and groups the text inside (parentheses), not including the parentheses
		// 'lighting (verse)' -> 'verse', lighting (flare_fast)' -> 'flare_fast', 'lighting ()' -> ''
		private static readonly Regex lightingRegex = new(@"lighting\s+\((.*?)\)", RegexOptions.Compiled | RegexOptions.Singleline);

		private void ParseVenue(List<EventIR> eventIR, TrackChunk trackChunk) {
			long totalDelta = 0;
            var noteQueue = new List<(NoteOnEvent note, long tick)>();

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is BaseTextEvent textEvent) {
					ProcessText(eventIR, textEvent.Text, totalDelta);
				} else if (trackEvent is NoteOnEvent noteOn) {
                    if (noteQueue.Any((queued) => queued.note.NoteNumber == noteOn.NoteNumber
						&& queued.note.Channel == noteOn.Channel)) {
						// Duplicate note
                        continue;
                    }
					noteQueue.Add((noteOn, totalDelta));
				} else if (trackEvent is NoteOffEvent noteOff) {
                    // Get note on event
                    long noteOnTime = 0;
                    var queued = noteQueue.FirstOrDefault((queued) => queued.note.NoteNumber == noteOff.NoteNumber
						&& queued.note.Channel == noteOff.Channel);
                    (noteOn, noteOnTime) = queued;
                    if (noteOn == null) {
						// No corresponding note-on
                        continue;
                    }

                    noteQueue.Remove(queued);
					ProcessNoteEvent(eventIR, noteOn, noteOnTime, totalDelta - noteOnTime);
				}
			}
		}

		private void ProcessNoteEvent(List<EventIR> eventIR, NoteOnEvent noteEvent, long startTick, long endTick) {
			// Handle notes that are equivalent to other text events
			string eventText = (byte)noteEvent.NoteNumber switch {
				// Lighting keyframes
				48 => "[next]",

				_ => null
			};

			if (eventText != null) {
				ProcessText(eventIR, eventText, startTick);
				return;
			}
		}

		private void ProcessText(List<EventIR> eventIR, string text, long eventTick) {
			// Strip away the [brackets] from events (and any garbage outside them)
			var match = textEventRegex.Match(text);
			if (match.Success) {
				text = match.Groups[1].Value;
			}

			// Turn text event into the event name the game should use
			string finalText = null;
			switch (text) {
				case "next": finalText = "venue_lightFrame_next"; break;

				case "verse": finalText = "venue_light_verse"; break;
				case "chorus": finalText = "venue_light_chorus"; break;

				default:
					// Venue lighting
					match = lightingRegex.Match(text);
					if (match.Success) {
						string lightingType = match.Groups[1].Value;
						if (string.IsNullOrWhiteSpace(lightingType)) {
							lightingType = "default";
						}
						finalText = $"venue_light_{lightingType}";
						break;
					}
					break;
			}

			if (finalText != null) {
				eventIR.Add(new EventIR {
					startTick = eventTick,
					name = finalText
				});
			}
		}
	}
}