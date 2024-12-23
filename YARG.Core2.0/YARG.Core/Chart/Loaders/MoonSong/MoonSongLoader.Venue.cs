using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Chart
{
    using static VenueLookup;

    internal partial class MoonSongLoader : ISongLoader
    {

        public VenueTrack LoadVenueTrack()
        {
            var lightingEvents = new List<LightingEvent>();
            var postProcessingEvents = new List<PostProcessingEvent>();
            var performerEvents = new List<PerformerEvent>();
            var stageEvents = new List<StageEffectEvent>();

            // For merging spotlights/singalongs into a single event
            MoonVenue? spotlightCurrentEvent = null;
            MoonVenue? singalongCurrentEvent = null;
            var spotlightPerformers = Performer.None;
            var singalongPerformers = Performer.None;

            foreach (var moonVenue in _moonSong.venue)
            {
                // Prefix flags
                var splitter = moonVenue.text.AsSpan().Split(' ');
                splitter.MoveNext();
                var flags = VenueEventFlags.None;
                foreach (var (prefix, flag) in FlagPrefixLookup)
                {
                    if (splitter.Current.Equals(prefix, StringComparison.Ordinal))
                    {
                        flags |= flag;
                        splitter.MoveNext();
                    }
                }

                // Taking the allocation L here, the only way to access with a span is by going over
                // all the key-value pairs, which is 5x slower at even just 25 elements (O(n) vs O(1) with a string)
                // There's a lot of other allocations happening here anyways lol
                string text = splitter.CurrentToEnd.ToString();
                switch (moonVenue.type)
                {
                    case VenueLookup.Type.Lighting:
                    {
                        if (!LightingLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        lightingEvents.Add(new(type, time, moonVenue.tick));
                        break;
                    }

                    case VenueLookup.Type.PostProcessing:
                    {
                        if (!PostProcessLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        postProcessingEvents.Add(new(type, time, moonVenue.tick));
                        break;
                    }

                    case VenueLookup.Type.Singalong:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Singalong, moonVenue,
                            ref singalongCurrentEvent, ref singalongPerformers);
                        break;
                    }

                    case VenueLookup.Type.Spotlight:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Spotlight, moonVenue,
                            ref spotlightCurrentEvent, ref spotlightPerformers);
                        break;
                    }

                    case VenueLookup.Type.StageEffect:
                    {
                        if (!StageEffectLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        stageEvents.Add(new(type, flags, time, moonVenue.tick));
                        break;
                    }

                    default:
                    {
                        YargLogger.LogFormatDebug("Unrecognized venue text event '{0}'!", text);
                        continue;
                    }
                }
            }

            lightingEvents.TrimExcess();
            postProcessingEvents.TrimExcess();
            performerEvents.TrimExcess();
            stageEvents.TrimExcess();

            return new(lightingEvents, postProcessingEvents, performerEvents, stageEvents);
        }

        private void HandlePerformerEvent(List<PerformerEvent> events, PerformerEventType type, MoonVenue moonEvent,
            ref MoonVenue? currentEvent, ref Performer performers)
        {
            // First event
            if (currentEvent == null)
            {
                currentEvent = moonEvent;
            }
            // Start of a new event
            else if (currentEvent.tick != moonEvent.tick && performers != Performer.None)
            {
                double time = _moonSong.TickToTime(currentEvent.tick);
                // Add tracked event
                events.Add(new(type, performers, time, GetLengthInTime(currentEvent),
                    currentEvent.tick, currentEvent.length));

                // Track new event
                currentEvent = moonEvent;
                performers = Performer.None;
            }

            // Sing-along events are not optional, use the text directly
            if (!PerformerLookup.TryGetValue(moonEvent.text, out var performer))
                return;
            performers |= performer;
        }

        private double GetLengthInTime(MoonVenue ev)
        {
            double time = _moonSong.TickToTime(ev.tick);
            return GetLengthInTime(time, ev.tick, ev.length);
        }
    }
}