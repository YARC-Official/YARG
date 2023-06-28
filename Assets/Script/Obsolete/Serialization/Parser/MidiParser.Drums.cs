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
        private enum CymbalState
        {
            NONE,
            NO_YELLOW,
            NO_BLUE,
            NO_GREEN
        }

        private enum DrumFlags
        {
            NONE,
            DISCO_FLIP
        }

        private struct CymbalStateIR
        {
            public float start;
            public float end;

            public CymbalState cymbalState;
        }

        private struct DrumFlagIR
        {
            public float start;

            public DrumFlags drumFlags;
        }

        private List<NoteInfo> ParseDrums(TrackChunk trackChunk, bool pro, int difficulty, DrumType drumType,
            List<NoteInfo> ghEquivalent)
        {
            var tempoMap = midi.GetTempoMap();

            if (drumType == DrumType.FourLane)
            {
                var notes = DrumNotePass(trackChunk, difficulty, tempoMap);

                if (pro)
                {
                    var cymbalStateIR = DrumCymbalStatePass(trackChunk, tempoMap);
                    var flagIR = DrumFlagPass(trackChunk, difficulty, tempoMap);
                    DrumNoteStatePass(notes, cymbalStateIR, flagIR);
                }

                return notes;
            }
            else
            {
                return DrumFromGH(ghEquivalent);
            }
        }

        private List<CymbalStateIR> DrumCymbalStatePass(TrackChunk trackChunk, TempoMap tempoMap)
        {
            long totalDelta = 0;

            var cymbalIR = new List<CymbalStateIR>();

            // Since each state has an ON and OFF event,
            // we must store the ON events and wait until the
            // OFF event to actually add the state. This stores
            // the ON event timings.
            long?[] cymbalStateArray = new long?[3];

            // Convert track events into intermediate representation
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                if (trackEvent is NoteEvent noteEvent)
                {
                    // Note based flags

                    // Look for correct octave
                    if (noteEvent.GetNoteOctave() != 8)
                    {
                        continue;
                    }

                    // Convert note to cymbal state (or special)
                    CymbalState cymbalState = noteEvent.GetNoteName() switch
                    {
                        // Green tom
                        NoteName.E => CymbalState.NO_GREEN,
                        // Blue tom
                        NoteName.DSharp => CymbalState.NO_BLUE,
                        // Yellow tom
                        NoteName.D => CymbalState.NO_YELLOW,
                        // Default
                        _ => CymbalState.NONE
                    };

                    // Skip if not an actual state
                    if (cymbalState == CymbalState.NONE)
                    {
                        continue;
                    }

                    // Deal with notes
                    int i = (int) cymbalState - 1;
                    if (noteEvent is NoteOnEvent)
                    {
                        // If it is a note on, wait until we get the note
                        // off so we can get the length of the note.
                        cymbalStateArray[i] = totalDelta;
                    }
                    else if (noteEvent is NoteOffEvent)
                    {
                        if (cymbalStateArray[i] == null)
                        {
                            continue;
                        }

                        cymbalIR.Add(new CymbalStateIR
                        {
                            start =
                                (float) TimeConverter.ConvertTo<MetricTimeSpan>(cymbalStateArray[i].Value, tempoMap)
                                    .TotalSeconds,
                            end = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds,
                            cymbalState = cymbalState
                        });

                        cymbalStateArray[i] = null;
                    }
                }
            }

            return cymbalIR;
        }

        private List<DrumFlagIR> DrumFlagPass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap)
        {
            // Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#important-text-events

            long totalDelta = 0;

            var flagIR = new List<DrumFlagIR>();

            // Expert+ is just Expert with double-kick (we don't really care about that here)
            if (difficulty == (int) Difficulty.EXPERT_PLUS)
            {
                difficulty = (int) Difficulty.EXPERT;
            }

            // Convert track events into intermediate representation
            foreach (var trackEvent in trackChunk.Events)
            {
                totalDelta += trackEvent.DeltaTime;

                // Drum state changes are stored in text events
                if (trackEvent is BaseTextEvent textEvent)
                {
                    // Split the text into sections
                    string[] split = textEvent.Text.Split(' ');

                    // Check if it is a mix flag
                    if (split[0] != "[mix")
                    {
                        continue;
                    }

                    // Next split should be an integer indicating difficulty
                    int readDifficulty;
                    try
                    {
                        readDifficulty = int.Parse(split[1]);
                    }
                    catch
                    {
                        continue;
                    }

                    // Check difficulty
                    if (readDifficulty != difficulty)
                    {
                        continue;
                    }

                    // Next split is the config + ]
                    string config = split[2][..^1];

                    // First five letters should be "drums"
                    if (!config.StartsWith("drums"))
                    {
                        continue;
                    }

                    config = config[5..];

                    // Next letter should be the mix flag (we don't care about this right now)
                    // int mix = 0;
                    try
                    {
                        /*mix = */
                        int.Parse(config[0].ToString());
                    }
                    catch
                    {
                        continue;
                    }

                    config = config[1..];

                    // Last part should be the flag
                    float startTime =
                        (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;
                    if (config == "")
                    {
                        flagIR.Add(new DrumFlagIR
                        {
                            start = startTime, drumFlags = DrumFlags.NONE
                        });
                    }
                    else if (config == "d")
                    {
                        flagIR.Add(new DrumFlagIR
                        {
                            start = startTime, drumFlags = DrumFlags.DISCO_FLIP
                        });
                    }
                }
            }

            return flagIR;
        }

        private List<NoteInfo> DrumNotePass(TrackChunk trackChunk, int difficulty, TempoMap tempoMap)
        {
            long totalDelta = 0;

            var noteOutput = new List<NoteInfo>();

            // Expert+ is just Expert with double-kick
            bool doubleKick = false;
            if (difficulty == (int) Difficulty.EXPERT_PLUS)
            {
                doubleKick = true;
                difficulty = (int) Difficulty.EXPERT;
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
                    // Orange (Kick)
                    NoteName.C => 4,
                    // Red
                    NoteName.CSharp => 0,
                    // Yellow
                    NoteName.D => 1,
                    // Blue
                    NoteName.DSharp => 2,
                    // Green
                    NoteName.E => 3,
                    // Default
                    _ => -1
                };

                // Skip if not an actual note
                if (drum == -1)
                {
                    continue;
                }

                // Check if cymbal
                bool isCymbal = drum switch
                {
                    1 or 2 or 3 => true,
                    _           => false
                };

                // Get start time (in seconds)
                float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempoMap).TotalSeconds;

                // Add note
                noteOutput.Add(new NoteInfo
                {
                    time = startTime, length = 0f, fret = drum, hopo = isCymbal
                });
            }

            return noteOutput;
        }

        private void DrumNoteStatePass(List<NoteInfo> noteIR, List<CymbalStateIR> cymbalStateIR,
            List<DrumFlagIR> drumFlagIR)
        {
            foreach (var note in noteIR)
            {
                // Disco flip
                if (note.fret == 0 || note.fret == 1)
                {
                    // See if we are in any disco flip ranges
                    bool isDisco = false;
                    foreach (var flagIR in drumFlagIR)
                    {
                        // If the flag is after the note, we are done
                        if (flagIR.start > note.time)
                        {
                            break;
                        }

                        // Go through everything and keep track of the state
                        if (flagIR.drumFlags == DrumFlags.DISCO_FLIP)
                        {
                            isDisco = true;
                        }
                        else if (flagIR.drumFlags == DrumFlags.NONE)
                        {
                            isDisco = false;
                        }
                    }

                    // Flip! (remeber that this only gets called in pro mode)
                    if (isDisco)
                    {
                        if (note.fret == 1)
                        {
                            // Red drum can't be a cymbal!
                            note.fret = 0;
                            note.hopo = false;
                        }
                        else
                        {
                            note.fret = 1;
                        }
                    }
                }

                // Cymbal state (only the toms)
                if (note.fret == 1 || note.fret == 2 || note.fret == 3)
                {
                    // See if we are in any tom force ranges
                    bool isTom = false;
                    foreach (var cymbalIR in cymbalStateIR)
                    {
                        if (cymbalIR.cymbalState != (CymbalState) note.fret)
                        {
                            continue;
                        }

                        if (note.time >= cymbalIR.start && note.time < cymbalIR.end)
                        {
                            isTom = true;
                            break;
                        }
                    }

                    // Set as tom
                    note.hopo = !isTom;
                }
            }
        }

        private List<NoteInfo> DrumFromGH(List<NoteInfo> ghNotes)
        {
            // Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/Drums.md#track-type-conversions

            var noteOutput = new List<NoteInfo>();

            // Convert from GH to Standard
            NoteInfo lastNote = null;
            foreach (var note in ghNotes)
            {
                var newNote = note.Duplicate();

                // Convert to 4-lane
                switch (newNote.fret)
                {
                    case 1: // Red -> Red Cymbal
                        newNote.fret = 1;
                        newNote.hopo = true;
                        break;
                    case 3: // Orange -> Green Cymbal
                        newNote.fret = 3;
                        newNote.hopo = true;
                        break;
                    case 4: // Green -> Green Tom
                        newNote.fret = 3;
                        newNote.hopo = false;
                        break;
                    case 5: // Kick
                        newNote.fret = 4;
                        break;
                }

                // Check for Green Cymbal + Green Tom collision
                if (lastNote != null && lastNote.time == newNote.time)
                {
                    if (lastNote.fret == 3 && lastNote.hopo &&
                        newNote.fret == 3 && !newNote.hopo)
                    {
                        newNote.fret = 2;
                        newNote.hopo = false;
                    }
                    else if (lastNote.fret == 3 && !lastNote.hopo &&
                        newNote.fret == 3 && newNote.hopo)
                    {
                        lastNote.fret = 2;
                        lastNote.hopo = false;
                    }
                }

                // Add note
                noteOutput.Add(newNote);
                lastNote = newNote;
            }

            return noteOutput;
        }
    }
}