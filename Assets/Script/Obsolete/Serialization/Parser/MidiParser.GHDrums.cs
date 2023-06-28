using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization.Parser
{
    public partial class MidiParser : AbstractParser
    {
        private List<NoteInfo> ParseGHDrums(TrackChunk trackChunk, int difficulty, DrumType drumType,
            List<NoteInfo> stdEquivalent)
        {
            var tempoMap = midi.GetTempoMap();

            if (drumType == DrumType.FiveLane)
            {
                return GHDrumNotePass(trackChunk, difficulty, tempoMap);
            }
            else
            {
                return DrumFromStandard(stdEquivalent);
            }
        }

        private List<NoteInfo> GHDrumNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap)
        {
            long totalDelta = 0;

            var noteOutput = new List<NoteInfo>();

            // Expert+ is just Expert with double-kick
            bool doubleKick = false;
            if (difficulty == (int) Difficulty.EXPERT_PLUS)
            {
                doubleKick = true;
                difficulty--;
            }

            // Convert track events into note info
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                if (trackEvent is not NoteOnEvent noteEvent)
                {
                    continue;
                }

                // Look for correct octave
                var noteName = noteEvent.GetNoteName();
                if (noteEvent.GetNoteOctave() != 4 + difficulty)
                {
                    if (doubleKick && noteEvent.GetNoteOctave() == 6 && noteName == NoteName.B)
                    {
                        // Set as kick if double-kick
                        noteName = NoteName.C;
                    }
                    else
                    {
                        continue;
                    }
                }

                // Convert note to drum number
                int drum = noteName switch
                {
                    // Kick
                    NoteName.C => 5,
                    // Red
                    NoteName.CSharp => 0,
                    // Yellow
                    NoteName.D => 1,
                    // Blue
                    NoteName.DSharp => 2,
                    // Orange
                    NoteName.E => 3,
                    // Green
                    NoteName.F => 4,
                    // Default
                    _ => -1
                };

                // Skip if not an actual note
                if (drum == -1)
                {
                    continue;
                }

                // Get start time (in seconds)
                float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

                // Add note
                noteOutput.Add(new NoteInfo
                {
                    time = startTime, length = 0f, fret = drum
                });
            }

            return noteOutput;
        }

        private List<NoteInfo> DrumFromStandard(List<NoteInfo> stdNotes)
        {
            // Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-type-conversions

            var noteOutput = new List<NoteInfo>();

            // Convert from GH to Standard
            NoteInfo lastNote = null;
            foreach (var note in stdNotes)
            {
                var newNote = note.Duplicate();

                // Convert to 5-lane
                switch (newNote.fret)
                {
                    case 1:
                        if (newNote.hopo)
                        {
                            // Yellow cymbal -> Yellow
                            newNote.fret = 1;
                            newNote.hopo = false;
                        }
                        else
                        {
                            // Yellow tom -> Blue

                            // Unless Yellow tom + Blue tom -> Red + Blue
                            if (lastNote != null && lastNote.time == newNote.time)
                            {
                                if (lastNote.fret == 2)
                                {
                                    newNote.fret = 0;
                                }
                                else
                                {
                                    newNote.fret = 2;
                                }
                            }
                            else
                            {
                                newNote.fret = 2;
                            }
                        }

                        break;
                    case 2:
                        if (newNote.hopo)
                        {
                            // Blue cymbal -> Orange
                            newNote.hopo = false;

                            // Unless Blue cymbal + Green cymbal -> Yellow + Orange
                            if (lastNote != null && lastNote.time == newNote.time)
                            {
                                if (lastNote.fret == 1)
                                {
                                    newNote.fret = 3;
                                }
                                else
                                {
                                    newNote.fret = 1;
                                }
                            }
                            else
                            {
                                newNote.fret = 1;
                            }
                        }
                        else
                        {
                            // Blue tom -> Blue

                            // Unless Yellow tom + Blue tom -> Red + Blue
                            if (lastNote != null && lastNote.time == newNote.time)
                            {
                                if (lastNote.fret == 2)
                                {
                                    newNote.fret = 0;
                                }
                                else
                                {
                                    newNote.fret = 2;
                                }
                            }
                            else
                            {
                                newNote.fret = 2;
                            }
                        }

                        break;
                    case 3:
                        if (newNote.hopo)
                        {
                            // Green cymbal -> Orange
                            newNote.hopo = false;

                            // Unless Blue cymbal + Green cymbal -> Yellow + Orange
                            if (lastNote != null && lastNote.time == newNote.time)
                            {
                                if (lastNote.fret == 3)
                                {
                                    newNote.fret = 1;
                                }
                                else
                                {
                                    newNote.fret = 3;
                                }
                            }
                            else
                            {
                                newNote.fret = 3;
                            }
                        }
                        else
                        {
                            // Green tom -> Green
                            newNote.fret = 4;
                        }

                        break;
                    case 4: // Kick
                        newNote.fret = 5;
                        break;
                }

                // Add note
                noteOutput.Add(newNote);
                lastNote = newNote;
            }

            return noteOutput;
        }
    }
}