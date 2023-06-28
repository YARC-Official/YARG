using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Core;

/* TODO
Parsing:
- Camera cuts, thinking we'll need a new info type for this one

Handling:
- Post-processing handling
- Performer spotlights
- Performer sing-alongs
- Bonus effects
*/

namespace YARG.Serialization.Parser
{
    public partial class MidiParser : AbstractParser
    {
        // Matches lighting events and groups the text inside (parentheses), not including the parentheses
        // 'lighting (verse)' -> 'verse', lighting (flare_fast)' -> 'flare_fast', 'lighting ()' -> ''
        private static readonly Regex lightingRegex =
            new(@"lighting\s+\((.*?)\)", RegexOptions.Compiled | RegexOptions.Singleline);

        private void ParseVenue(List<EventIR> eventIR, TrackChunk trackChunk)
        {
            long totalDelta = 0;
            var noteQueue = new List<(NoteOnEvent note, long tick)>();

            // Convert track events into intermediate representation
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                if (trackEvent is BaseTextEvent textEvent)
                {
                    ProcessText(eventIR, textEvent.Text, totalDelta);
                }
                else if (trackEvent is NoteOnEvent noteOn)
                {
                    if (noteQueue.Any((queued) => queued.note.NoteNumber == noteOn.NoteNumber
                        && queued.note.Channel == noteOn.Channel))
                    {
                        // Duplicate note
                        continue;
                    }

                    noteQueue.Add((noteOn, totalDelta));
                }
                else if (trackEvent is NoteOffEvent noteOff)
                {
                    // Get note on event
                    long noteOnTime = 0;
                    var queued = noteQueue.FirstOrDefault((queued) => queued.note.NoteNumber == noteOff.NoteNumber
                        && queued.note.Channel == noteOff.Channel);
                    (noteOn, noteOnTime) = queued;
                    if (noteOn == null)
                    {
                        // No corresponding note-on
                        continue;
                    }

                    noteQueue.Remove(queued);
                    ProcessNoteEvent(eventIR, noteOn, noteOnTime, totalDelta - noteOnTime);
                }
            }
        }

        private void ProcessNoteEvent(List<EventIR> eventIR, NoteOnEvent noteEvent, long startTick, long endTick)
        {
            // Handle notes that are equivalent to other text events
            string eventText = (byte) noteEvent.NoteNumber switch
            {
                // Post-processing
                110 => "[video_trails.pp]",
                109 => "[video_security.pp]",
                108 => "[video_bw.pp]",
                107 => "[video_a.pp]",
                106 => "[film_blue_filter.pp]",
                105 => "[ProFilm_mirror_a.pp]",
                104 => "[ProFilm_b.pp]",
                103 => "[ProFilm_a.pp]",
                102 => "[photocopy.pp]",
                101 => "[photo_negative.pp]",
                100 => "[film_silvertone.pp]",
                99  => "[film_sepia_ink.pp]",
                98  => "[film_16mm.pp]",
                97  => "[contrast_a.pp]",
                96  => "[ProFilm_a.pp]",

                // Lighting keyframes
                50 => "[first]",
                49 => "[prev]",
                48 => "[next]",

                _ => null
            };

            if (eventText != null)
            {
                ProcessText(eventIR, eventText, startTick);
                return;
            }

            // Handle events with length
            eventText = (byte) noteEvent.NoteNumber switch
            {
                // Performer sing-alongs
                87 => "venue_singalong_guitarOrKeys",
                86 => "venue_singalong_drums",
                85 => "venue_singalong_bassOrKeys",

                // Performer spotlights
                41 => "venue_spotlight_keys",
                40 => "venue_spotlight_vocals",
                39 => "venue_spotlight_guitar",
                38 => "venue_spotlight_drums",
                37 => "venue_spotlight_bass",

                _ => null
            };

            if (eventText != null)
            {
                eventIR.Add(new EventIR
                {
                    startTick = startTick, endTick = endTick, name = eventText
                });
                return;
            }

            // TODO: Camera cuts
        }

        private void ProcessText(List<EventIR> eventIR, string text, long eventTick)
        {
            // Strip away the [brackets] from events (and any garbage outside them)
            var match = textEventRegex.Match(text);
            if (match.Success)
            {
                text = match.Groups[1].Value;
            }

            // Turn text event into the event name the game should use
            string finalText = null;
            switch (text)
            {
                case "FogOn":
                    finalText = "venue_fog_on";
                    break;
                case "FogOff":
                    finalText = "venue_fog_off";
                    break;

                case "next":
                    finalText = "venue_lightFrame_next";
                    break;
                case "prev":
                    finalText = "venue_lightFrame_previous";
                    break;
                case "first":
                    finalText = "venue_lightFrame_first";
                    break;

                case "verse":
                    finalText = "venue_light_verse";
                    break;
                case "chorus":
                    finalText = "venue_light_chorus";
                    break;

                case "bonusfx":
                    finalText = "venue_bonus_fx";
                    break;
                case "bonusfx_optional":
                    finalText = "venue_bonus_fx_optional";
                    break;

                default:
                    // Venue lighting
                    match = lightingRegex.Match(text);
                    if (match.Success)
                    {
                        string lightingType = match.Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(lightingType))
                        {
                            lightingType = "default";
                        }

                        finalText = $"venue_light_{lightingType}";
                        break;
                    }

                    // Post-processing
                    if (text.EndsWith(".pp"))
                    {
                        finalText = $"venue_postProcess_{text.Replace(".pp", "")}";
                        break;
                    }

                    break;
            }

            if (finalText != null)
            {
                eventIR.Add(new EventIR
                {
                    startTick = eventTick, name = finalText
                });
            }
        }
    }
}