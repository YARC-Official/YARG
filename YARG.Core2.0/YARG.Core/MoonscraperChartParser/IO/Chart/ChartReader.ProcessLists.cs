// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Parsing;

namespace MoonscraperChartEditor.Song.IO
{
    internal static partial class ChartReader
    {
        private delegate void NoteEventProcessFn(ref NoteProcessParams noteProcessParams);

        // These dictionaries map the number of a note event to a specific function of how to process them
        private static readonly Dictionary<int, NoteEventProcessFn> GuitarChartNoteNumberToProcessFnMap = new()
        {
            { 0, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Green); }},
            { 1, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Red); }},
            { 2, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Yellow); }},
            { 3, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Blue); }},
            { 4, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Orange); }},
            { 7, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GuitarFret.Open); }},

            { 5, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(ref noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(ref noteProcessParams, NoteFlagPriority.Tap); }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> DrumsChartNoteNumberToProcessFnMap = new()
        {
            { 0, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Kick); }},
            { 1, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Red); }},
            { 2, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Yellow); }},
            { 3, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Blue); }},
            { 4, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Orange); }},
            { 5, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Green); }},

            { ChartIOHelper.NOTE_OFFSET_INSTRUMENT_PLUS, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.DrumPad.Kick, MoonNote.Flags.DoubleKick);
            } },

            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 3, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 4, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Cymbal);
            } },

            // { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 0, ... }  // Reserved for kick accents, if they should ever be a thing
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 1, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 3, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 4, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 5, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Accent);
            } },

            // { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 0, ... }  // Reserved for kick ghosts, if they should ever be a thing
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 1, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 3, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 4, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 5, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(ref noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Ghost);
            } },
        };

        private static readonly Dictionary<int, NoteEventProcessFn> GhlChartNoteNumberToProcessFnMap = new()
        {
            { 0, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White1); }},
            { 1, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White2); }},
            { 2, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White3); }},
            { 3, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black1); }},
            { 4, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black2); }},
            { 8, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black3); }},
            { 7, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(ref noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Open); }},

            { 5, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(ref noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (ref NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(ref noteProcessParams, NoteFlagPriority.Tap); }},
        };

        // These dictionaries map the number of a special phrase event to a specific function of how to process them
        // Not all tracks support the same phrases, so this is done for flexibility
        private static readonly Dictionary<int, NoteEventProcessFn> GuitarChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Starpower);
            }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> DrumsChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Starpower);
            }},
            { ChartIOHelper.PHRASE_DRUM_FILL, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.ProDrums_Activation);
            }},
            { ChartIOHelper.PHRASE_TREMOLO_LANE, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.TremoloLane);
            }},
            { ChartIOHelper.PHRASE_TRILL_LANE, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.TrillLane);
            }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> GhlChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (ref NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(ref noteProcessParams, MoonPhrase.Type.Starpower);
            }},
        };

        // Initial post-processing list
        private static readonly List<NoteEventProcessFn> GuitarInitialPostProcessList = new()
        {
            ConvertSoloEvents,
        };

        private static readonly List<NoteEventProcessFn> DrumsInitialPostProcessList = new()
        {
            ConvertSoloEvents,
            DisambiguateDrumsType,
        };

        private static readonly List<NoteEventProcessFn> GhlGuitarInitialPostProcessList = new()
        {
            ConvertSoloEvents,
        };

        private static Dictionary<int, NoteEventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarChartNoteNumberToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlChartNoteNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsChartNoteNumberToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static Dictionary<int, NoteEventProcessFn> GetSpecialPhraseProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarChartSpecialPhraseNumberToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlChartSpecialPhraseNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsChartSpecialPhraseNumberToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static List<NoteEventProcessFn> GetInitialPostProcessList(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => new(GuitarInitialPostProcessList),
                MoonChart.GameMode.GHLGuitar => new(GhlGuitarInitialPostProcessList),
                MoonChart.GameMode.Drums => new(DrumsInitialPostProcessList),
                _ => throw new NotImplementedException($"No process list for game mode {gameMode}!")
            };
        }

        private static void ConvertSoloEvents(ref NoteProcessParams noteProcessParams)
        {
            var chart = noteProcessParams.chart;
            // Keeps tracks of soloes that start on the same tick when another solo ends
            uint startTick = uint.MaxValue;
            uint nextStartTick = uint.MaxValue;
            for (int i = 0; i < chart.events.Count; ++i)
            {
                var ev = chart.events[i];
                if (ev.text == TextEvents.SOLO_START)
                {
                    if (startTick == uint.MaxValue)
                    {
                        startTick = ev.tick;
                    }
                    else
                    {
                        nextStartTick = ev.tick;
                    }
                }
                else if (ev.text == TextEvents.SOLO_END)
                {
                    if (startTick != uint.MaxValue)
                    {
                        // .chart handles solo phrases with *inclusive ends*, so we have to add one tick.
                        // The only exception will be if another solo starts on the same exact tick.
                        //
                        // Comparing to the current tick instead of against uint.MaxValue ensures
                        // that the we don't allow overlaps
                        if (nextStartTick != ev.tick)
                        {
                            chart.Add(new MoonPhrase(startTick, ev.tick + 1 - startTick, MoonPhrase.Type.Solo));
                            startTick = uint.MaxValue;
                        }
                        else
                        {
                            chart.Add(new MoonPhrase(startTick, ev.tick - startTick, MoonPhrase.Type.Solo));
                            startTick = nextStartTick;
                            nextStartTick = uint.MaxValue;
                        }
                    }
                }
            }
        }

        private static void DisambiguateDrumsType(ref NoteProcessParams processParams)
        {
            if (processParams.settings.DrumsType is not DrumsType.Unknown)
                return;

            foreach (var note in processParams.chart.notes)
            {
                // Cymbal markers indicate 4-lane
                if ((note.flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
                {
                    processParams.settings.DrumsType = DrumsType.FourLane;
                    return;
                }

                // 5-lane green indicates 5-lane
                if (note.drumPad is MoonNote.DrumPad.Green)
                {
                    processParams.settings.DrumsType = DrumsType.FiveLane;
                    return;
                }
            }

            // Assume 4-lane if otherwise undetermined
            processParams.settings.DrumsType = DrumsType.FourLane;
        }
    }
}