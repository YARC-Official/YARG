using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;

namespace YARG.Serialization.Parser
{
    public partial class MidiParser : AbstractParser
    {
        private List<NoteInfo> ParseRealGuitar(TrackChunk trackChunk, int difficulty)
        {
            var tempoMap = midi.GetTempoMap();

            var noteOutput = RealGuitarNotePass(trackChunk, difficulty, tempoMap);

            return noteOutput;
        }

        private List<NoteInfo> RealGuitarNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap)
        {
            long totalDelta = 0;

            var notes = new List<NoteInfo>();
            var currentChord = new NoteInfo();

            // Since each note has an ON and OFF event,
            // we must store the ON events and wait until the
            // OFF event to actually add the note. This stores
            // the ON event timings.
            float?[] strState = new float?[6];

            // Do the same with velocity
            int[] velocity = new int[6];

            // Convert track events into intermediate representation
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                if (trackEvent is not NoteEvent noteEvent)
                {
                    continue;
                }

                // Look for correct octave
                if (noteEvent.GetNoteOctave() != 1 + difficulty * 2)
                {
                    continue;
                }

                // Convert note to string number
                int str = noteEvent.GetNoteName() switch
                {
                    NoteName.C      => 0,
                    NoteName.CSharp => 1,
                    NoteName.D      => 2,
                    NoteName.DSharp => 3,
                    NoteName.E      => 4,
                    NoteName.F      => 5,
                    _               => -1
                };

                // Skip if not an actual note
                if (str == -1)
                {
                    continue;
                }

                // Deal with notes
                if (noteEvent is NoteOnEvent)
                {
                    // If it is a note on, wait until we get the note
                    // off so we can get the length of the note.
                    var time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;
                    strState[str] = time;
                    velocity[str] = noteEvent.Velocity;
                }
                else if (noteEvent is NoteOffEvent)
                {
                    // Here is were the notes are actually stored.
                    // We now know the starting point and ending point.

                    if (strState[str] == null)
                    {
                        continue;
                    }

                    int fret = velocity[str] - 100;

                    // Collect the notes in chords.
                    // If the chord is complete, add it.
                    if (currentChord.time != strState[str])
                    {
                        notes.Add(currentChord);

                        float t = strState[str].Value;
                        currentChord = new NoteInfo
                        {
                            time = t,
                            length =
                                (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds - t,
                            stringFrets = new int[]
                            {
                                -1, -1, -1, -1, -1, -1
                            },
                            muted = noteEvent.Channel == 3
                        };

                        currentChord.stringFrets[str] = fret;
                    }
                    else
                    {
                        currentChord.stringFrets[str] = fret;
                    }

                    // Reset string state and wait until next ON event
                    strState[str] = null;
                }
            }

            // Remove the first note IR as it is empty
            notes.Add(currentChord);
            notes.RemoveAt(0);

            return notes;
        }
    }
}