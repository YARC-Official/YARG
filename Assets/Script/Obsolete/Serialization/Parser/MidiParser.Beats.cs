using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace YARG.Serialization.Parser
{
    public partial class MidiParser : AbstractParser
    {
        private void ParseBeats(List<EventIR> eventIR, TrackChunk trackChunk)
        {
            long totalDelta = 0;

            // Convert track events into intermediate representation
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                if (trackEvent is not NoteOnEvent noteOnEvent)
                {
                    continue;
                }

                // Convert note to beat line type
                int majorOrMinor = noteOnEvent.GetNoteName() switch
                {
                    NoteName.C      => 0,
                    NoteName.CSharp => 1,
                    _               => -1
                };

                // Skip if not a beat line
                if (majorOrMinor == -1)
                {
                    continue;
                }

                if (majorOrMinor == 1)
                {
                    eventIR.Add(new EventIR
                    {
                        startTick = totalDelta, name = "beatLine_minor"
                    });
                }
                else
                {
                    eventIR.Add(new EventIR
                    {
                        startTick = totalDelta, name = "beatLine_major"
                    });
                }
            }
        }

        private void GenerateBeats(List<EventIR> eventIR, TempoMap tempo, float lastNoteTime)
        {
            int quatersIn = 0;
            float currentTime;
            do
            {
                // Get the time of the next beat line
                var musicalTime = MusicalTimeSpan.Quarter * quatersIn;
                var time = TimeConverter.ConvertFrom(musicalTime, tempo);

                // Check time signature to see if it is a major or minor beatline
                if (quatersIn % tempo.GetTimeSignatureAtTime(musicalTime).Numerator == 0)
                {
                    eventIR.Add(new EventIR
                    {
                        startTick = time, name = "beatLine_major"
                    });
                }
                else
                {
                    eventIR.Add(new EventIR
                    {
                        startTick = time, name = "beatLine_minor"
                    });
                }

                // Update info
                currentTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(time, tempo).TotalSeconds;
                quatersIn++;
            } while (currentTime < lastNoteTime);
        }
    }
}