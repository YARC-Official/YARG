// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace MoonscraperChartEditor.Song.IO
{
    internal static partial class MidReader
    {
        private static readonly List<MoonSong.MoonInstrument> LegacyStarPowerFixupWhitelist = new()
        {
            MoonSong.MoonInstrument.Guitar,
            MoonSong.MoonInstrument.GuitarCoop,
            MoonSong.MoonInstrument.Bass,
            MoonSong.MoonInstrument.Rhythm,
        };

        // Delegate for functions that parse something into the chart
        private delegate void EventProcessFn(ref EventProcessParams eventProcessParams);
        // Delegate for functions that modify how the chart should be parsed
        private delegate void ProcessModificationProcessFn(ref EventProcessParams eventProcessParams);

        private struct CommonPhraseSettings
        {
            public int starPowerNote;
            public int soloNote;
            public bool versusPhrases;
            public bool lanePhrases;
        }

        // These dictionaries map the NoteNumber of each midi note event to a specific function of how to process them
        private static readonly Dictionary<int, EventProcessFn> GuitarNoteProcessMap = BuildGuitarNoteProcessDict(enhancedOpens: false);
        private static readonly Dictionary<int, EventProcessFn> GuitarNoteProcessMap_EnhancedOpens = BuildGuitarNoteProcessDict(enhancedOpens: true);
        private static readonly Dictionary<int, EventProcessFn> GhlGuitarNoteProcessMap = BuildGhlGuitarNoteProcessDict();
        private static readonly Dictionary<int, EventProcessFn> ProGuitarNoteProcessMap = BuildProGuitarNoteProcessDict();
        private static readonly Dictionary<int, EventProcessFn> DrumsNoteProcessMap = BuildDrumsNoteProcessDict(enableVelocity: false);
        private static readonly Dictionary<int, EventProcessFn> DrumsNoteProcessMap_Velocity = BuildDrumsNoteProcessDict(enableVelocity: true);
        private static readonly Dictionary<int, EventProcessFn> VocalsNoteProcessMap = BuildVocalsNoteProcessDict();
        private static readonly Dictionary<int, EventProcessFn> ProKeysNoteProcessMap = BuildProKeysNoteProcessDict();

        private static readonly CommonPhraseSettings GuitarPhraseSettings = new()
        {
            soloNote = MidIOHelper.SOLO_NOTE,
            versusPhrases = true,
            lanePhrases = true,
        };

        private static readonly CommonPhraseSettings GhlGuitarPhraseSettings = new()
        {
            soloNote = MidIOHelper.SOLO_NOTE,
            versusPhrases = false,
            lanePhrases = false,
        };

        private static readonly CommonPhraseSettings ProGuitarPhraseSettings = new()
        {
            soloNote = MidIOHelper.SOLO_NOTE_PRO_GUITAR,
            versusPhrases = false,
            lanePhrases = true,
        };

        private static readonly CommonPhraseSettings DrumsPhraseSettings = new()
        {
            soloNote = MidIOHelper.SOLO_NOTE,
            versusPhrases = true,
            lanePhrases = true,
        };

        private static readonly CommonPhraseSettings VocalsPhraseSettings = new()
        {
            soloNote = -1,
            versusPhrases = false,
            lanePhrases = false,
        };

        private static readonly CommonPhraseSettings ProKeysPhraseSettings = new()
        {
            soloNote = MidIOHelper.SOLO_NOTE_PRO_KEYS,
            versusPhrases = false,
            // lanePhrases = true, // Handled manually due to per-difficulty tracks
        };

        // These dictionaries map the text of a MIDI text event to a specific function that processes them
        private static readonly Dictionary<string, ProcessModificationProcessFn> GuitarTextProcessMap = new()
        {
            { MidIOHelper.ENHANCED_OPENS_TEXT, SwitchToGuitarEnhancedOpensProcessMap },
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> GhlGuitarTextProcessMap = new()
        {
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> ProGuitarTextProcessMap = new()
        {
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> DrumsTextProcessMap = new()
        {
            { MidIOHelper.CHART_DYNAMICS_TEXT, SwitchToDrumsVelocityProcessMap },
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> VocalsTextProcessMap = new()
        {
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> ProKeysTextProcessMap = new()
        {
        };

        // These dictionaries map the phrase code of a SysEx event to a specific function that processes them
        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GuitarSysExProcessMap = new()
        {
            { PhaseShiftSysEx.PhraseCode.Guitar_Open, ProcessSysExEventPairAsOpenNoteModifier },
            { PhaseShiftSysEx.PhraseCode.Guitar_Tap, (ref EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsGuitarForcedType(ref eventProcessParams, MoonNote.MoonNoteType.Tap);
            }},
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GhlGuitarSysExProcessMap = new()
        {
            { PhaseShiftSysEx.PhraseCode.Guitar_Open, ProcessSysExEventPairAsOpenNoteModifier },
            { PhaseShiftSysEx.PhraseCode.Guitar_Tap, (ref EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsGuitarForcedType(ref eventProcessParams, MoonNote.MoonNoteType.Tap);
            }},
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> ProGuitarSysExProcessMap = new()
        {
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> DrumsSysExProcessMap = new()
        {
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> VocalsSysExProcessMap = new()
        {
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> ProKeysSysExProcessMap = new()
        {
        };

        // Some post-processing events should always be carried out on certain tracks
        private static readonly List<EventProcessFn> GuitarPostProcessList = new()
        {
            FixupStarPowerIfNeeded,
        };

        private static readonly List<EventProcessFn> GhlGuitarPostProcessList = new()
        {
        };

        private static readonly List<EventProcessFn> ProGuitarPostProcessList = new()
        {
        };

        private static readonly List<EventProcessFn> DrumsPostProcessList = new()
        {
            DisambiguateDrumsType,
        };

        private static readonly List<EventProcessFn> VocalsPostProcessList = new()
        {
            CopyDownHarmonyPhrases,
        };

        private static readonly List<EventProcessFn> ProKeysPostProcessList = new()
        {
        };

        private static Dictionary<int, EventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarNoteProcessMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarNoteProcessMap,
                MoonChart.GameMode.ProGuitar => ProGuitarNoteProcessMap,
                MoonChart.GameMode.Drums => DrumsNoteProcessMap,
                MoonChart.GameMode.Vocals => VocalsNoteProcessMap,
                MoonChart.GameMode.ProKeys => ProKeysNoteProcessMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static Dictionary<int, EventProcessFn> GetPhraseProcessDict(int spNote, MoonChart.GameMode gameMode)
        {
            // Set default if no override given
            if (spNote < 0)
                spNote = MidIOHelper.STARPOWER_NOTE;

            var phraseSettings = gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarPhraseSettings,
                MoonChart.GameMode.GHLGuitar => GhlGuitarPhraseSettings,
                MoonChart.GameMode.ProGuitar => ProGuitarPhraseSettings,
                MoonChart.GameMode.Drums => DrumsPhraseSettings,
                MoonChart.GameMode.Vocals => VocalsPhraseSettings,
                MoonChart.GameMode.ProKeys => ProKeysPhraseSettings,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
            phraseSettings.starPowerNote = spNote;

            // Don't add solos when SP is overridden to the legacy SP note
            if (phraseSettings.soloNote == spNote)
                phraseSettings.soloNote = -1;

            return BuildCommonPhraseProcessMap(phraseSettings);
        }

        private static Dictionary<string, ProcessModificationProcessFn> GetTextEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarTextProcessMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarTextProcessMap,
                MoonChart.GameMode.ProGuitar => ProGuitarTextProcessMap,
                MoonChart.GameMode.Drums => DrumsTextProcessMap,
                MoonChart.GameMode.Vocals => VocalsTextProcessMap,
                MoonChart.GameMode.ProKeys => ProKeysTextProcessMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GetSysExEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarSysExProcessMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarSysExProcessMap,
                MoonChart.GameMode.ProGuitar => ProGuitarSysExProcessMap,
                MoonChart.GameMode.Drums => DrumsSysExProcessMap,
                MoonChart.GameMode.Vocals => VocalsSysExProcessMap,
                MoonChart.GameMode.ProKeys => ProKeysSysExProcessMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static IReadOnlyList<EventProcessFn> GetPostProcessList(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarPostProcessList,
                MoonChart.GameMode.GHLGuitar => GhlGuitarPostProcessList,
                MoonChart.GameMode.ProGuitar => ProGuitarPostProcessList,
                MoonChart.GameMode.Drums => DrumsPostProcessList,
                MoonChart.GameMode.Vocals => VocalsPostProcessList,
                MoonChart.GameMode.ProKeys => ProKeysPostProcessList,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static void FixupStarPowerIfNeeded(ref EventProcessParams processParams)
        {
            // Ignore if SP override is specified
            if (processParams.settings.StarPowerNote >= 0)
                return;

            // Check if instrument is allowed to be fixed up
            if (!LegacyStarPowerFixupWhitelist.Contains(processParams.instrument))
                return;

            // Only need to check one difficulty since phrases get copied to all difficulties
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            if (chart.specialPhrases.Any((sp) => sp.type == MoonPhrase.Type.Starpower)
                || !chart.specialPhrases.Any((sp) => sp.type == MoonPhrase.Type.Solo))
            {
                return;
            }

            foreach (var diff in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                chart = processParams.song.GetChart(processParams.instrument, diff);
                foreach (var phrase in chart.specialPhrases)
                {
                    if (phrase.type == MoonPhrase.Type.Solo)
                        phrase.type = MoonPhrase.Type.Starpower;
                }
            }
        }

        private static void DisambiguateDrumsType(ref EventProcessParams processParams)
        {
            if (processParams.settings.DrumsType is not DrumsType.Unknown)
                return;

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                var chart = processParams.song.GetChart(processParams.instrument, difficulty);
                foreach (var note in chart.notes)
                {
                    // Tom markers indicate 4-lane
                    if (note.drumPad is not MoonNote.DrumPad.Red &&
                        (note.flags & MoonNote.Flags.ProDrums_Cymbal) == 0)
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
            }

            // Assume 4-lane if otherwise undetermined
            processParams.settings.DrumsType = DrumsType.FourLane;
        }

        private static void CopyDownHarmonyPhrases(ref EventProcessParams processParams)
        {
            if (processParams.instrument is not (MoonSong.MoonInstrument.Harmony2 or MoonSong.MoonInstrument.Harmony3))
                return;

            // Remove any existing phrases
            // TODO: HARM2 phrases are used to mark when lyrics shift in static lyrics, this needs to be preserved in some way
            // TODO: Determine if there are any phrases that shouldn't be removed/copied down
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            chart.specialPhrases.Clear();

            // Add in phrases from HARM1
            var harm1 = processParams.song.GetChart(MoonSong.MoonInstrument.Harmony1, MoonSong.Difficulty.Expert);
            foreach (var phrase in harm1.specialPhrases)
            {
                // Make a new copy instead of adding the original reference
                chart.specialPhrases.Add(phrase.Clone());
            }
        }

        private static void SwitchToGuitarEnhancedOpensProcessMap(ref EventProcessParams processParams)
        {
            var gameMode = MoonSong.InstrumentToChartGameMode(processParams.instrument);
            if (gameMode != MoonChart.GameMode.Guitar)
            {
                YargLogger.LogFormatWarning("Attempted to apply guitar enhanced opens process map to non-guitar instrument: {0}", processParams.instrument);
                return;
            }

            // Switch process map to guitar enhanced opens process map
            processParams.noteProcessMap = GuitarNoteProcessMap_EnhancedOpens;
        }

        private static void SwitchToDrumsVelocityProcessMap(ref EventProcessParams processParams)
        {
            var gameMode = MoonSong.InstrumentToChartGameMode(processParams.instrument);
            if (gameMode != MoonChart.GameMode.Drums)
            {
                YargLogger.LogFormatWarning("Attempted to apply drums velocity process map to non-drums instrument: {0}", processParams.instrument);
                return;
            }

            // Switch process map to drums velocity process map
            processParams.noteProcessMap = DrumsNoteProcessMap_Velocity;
        }

        private static Dictionary<int, EventProcessFn> BuildCommonPhraseProcessMap(CommonPhraseSettings settings)
        {
            var processMap = new Dictionary<int, EventProcessFn>();

            if (settings.starPowerNote >= 0)
            {
                processMap.Add(settings.starPowerNote, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Starpower);
                });
            }

            if (settings.soloNote >= 0)
            {
                processMap.Add(settings.soloNote, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Solo);
                });
            }

            if (settings.versusPhrases)
            {
                processMap.Add(MidIOHelper.VERSUS_PHRASE_PLAYER_1, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Versus_Player1);
                });
                processMap.Add(MidIOHelper.VERSUS_PHRASE_PLAYER_2, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Versus_Player2);
                });
            }

            if (settings.lanePhrases)
            {
                static void ProcessLanePhrase(ref EventProcessParams processParams, MoonPhrase.Type phraseType)
                {
                    if (processParams.timedEvent.midiEvent is not NoteEvent noteEvent)
                    {
                        YargLogger.FailFormat("Wrong note event type! Expected: {0}, Actual: {1}",
                            typeof(NoteEvent), processParams.timedEvent.midiEvent.GetType());
                        return;
                    }

                    ProcessNoteOnEventAsSpecialPhrase(ref processParams, phraseType, MoonSong.Difficulty.Expert);
                    if ((int)noteEvent.Velocity is >= 41 and <= 50)
                    {
                        ProcessNoteOnEventAsSpecialPhrase(ref processParams, phraseType, MoonSong.Difficulty.Hard);
                    }
                }

                processMap.Add(MidIOHelper.TREMOLO_LANE_NOTE, (ref EventProcessParams eventProcessParams) => {
                    ProcessLanePhrase(ref eventProcessParams, MoonPhrase.Type.TremoloLane);
                });
                processMap.Add(MidIOHelper.TRILL_LANE_NOTE, (ref EventProcessParams eventProcessParams) => {
                    ProcessLanePhrase(ref eventProcessParams, MoonPhrase.Type.TrillLane);
                });
            }

            return processMap;
        }

        private static Dictionary<int, EventProcessFn> BuildGuitarNoteProcessDict(bool enhancedOpens = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.TAP_NOTE_CH, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
            };

            var FretToMidiKey = new Dictionary<MoonNote.GuitarFret, int>()
            {
                { MoonNote.GuitarFret.Green, 0 },
                { MoonNote.GuitarFret.Red, 1 },
                { MoonNote.GuitarFret.Yellow, 2 },
                { MoonNote.GuitarFret.Blue, 3 },
                { MoonNote.GuitarFret.Orange, 4 },
            };

            if (enhancedOpens)
                FretToMidiKey.Add(MoonNote.GuitarFret.Open, -1);

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumExtensions<MoonNote.GuitarFret>.Values)
                {
                    if (FretToMidiKey.TryGetValue(guitarFret, out int fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 5;
                    processFnDict.Add(flagKey, (ref EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 6;
                    processFnDict.Add(flagKey, (ref EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, difficulty, MoonNote.MoonNoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildGhlGuitarNoteProcessDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.TAP_NOTE_CH, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
            };

            var FretToMidiKey = new Dictionary<MoonNote.GHLiveGuitarFret, int>()
            {
                { MoonNote.GHLiveGuitarFret.Open, 0 },
                { MoonNote.GHLiveGuitarFret.White1, 1 },
                { MoonNote.GHLiveGuitarFret.White2, 2 },
                { MoonNote.GHLiveGuitarFret.White3, 3 },
                { MoonNote.GHLiveGuitarFret.Black1, 4 },
                { MoonNote.GHLiveGuitarFret.Black2, 5 },
                { MoonNote.GHLiveGuitarFret.Black3, 6 },
            };

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GHL_GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumExtensions<MoonNote.GHLiveGuitarFret>.Values)
                {
                    if (FretToMidiKey.TryGetValue(guitarFret, out int fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 7;
                    processFnDict.Add(flagKey, (ref EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 8;
                    processFnDict.Add(flagKey, (ref EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, difficulty, MoonNote.MoonNoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildProGuitarNoteProcessDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
            };

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.PRO_GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var proString in EnumExtensions<MoonNote.ProGuitarString>.Values)
                {
                    int key = (int)proString + difficultyStartRange;
                    processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                    {
                        if (eventProcessParams.timedEvent.midiEvent is not NoteEvent noteEvent)
                        {
                            YargLogger.FailFormat("Wrong note event type! Expected: {0}, Actual: {1}",
                                typeof(NoteEvent), eventProcessParams.timedEvent.midiEvent.GetType());
                            return;
                        }

                        if (noteEvent.Velocity < 100)
                        {
                            YargLogger.LogFormatWarning("Encountered Pro Guitar note with invalid fret velocity {0}! Must be at least 100", noteEvent.Velocity);
                            return;
                        }

                        int fret = noteEvent.Velocity - 100;
                        int rawNote = MoonNote.MakeProGuitarRawNote(proString, fret);
                        if (!MidIOHelper.PRO_GUITAR_CHANNEL_FLAG_LOOKUP.TryGetValue(noteEvent.Channel, out var flags))
                            flags = MoonNote.Flags.None;

                        ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, rawNote, flags);
                    });
                }

                // Process forced hopo
                processFnDict.Add(difficultyStartRange + 6, (ref EventProcessParams eventProcessParams) =>
                {
                    ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                });
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildDrumsNoteProcessDict(bool enableVelocity = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.DRUM_FILL_NOTE_0, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_1, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_2, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_3, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_4, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.ProDrums_Activation);
                }},
            };

            var DrumPadToMidiKey = new Dictionary<MoonNote.DrumPad, int>()
            {
                { MoonNote.DrumPad.Kick, 0 },
                { MoonNote.DrumPad.Red, 1 },
                { MoonNote.DrumPad.Yellow, 2 },
                { MoonNote.DrumPad.Blue, 3 },
                { MoonNote.DrumPad.Orange, 4 },
                { MoonNote.DrumPad.Green, 5 },
            };

            var DrumPadDefaultFlags = new Dictionary<MoonNote.DrumPad, MoonNote.Flags>()
            {
                { MoonNote.DrumPad.Yellow, MoonNote.Flags.ProDrums_Cymbal },
                { MoonNote.DrumPad.Blue, MoonNote.Flags.ProDrums_Cymbal },
                { MoonNote.DrumPad.Orange, MoonNote.Flags.ProDrums_Cymbal },
            };

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.DRUMS_DIFF_START_LOOKUP[difficulty];
                foreach (var pad in EnumExtensions<MoonNote.DrumPad>.Values)
                {
                    if (DrumPadToMidiKey.TryGetValue(pad, out int padOffset))
                    {
                        int key = padOffset + difficultyStartRange;
                        int fret = (int)pad;
                        var defaultFlags = MoonNote.Flags.None;
                        DrumPadDefaultFlags.TryGetValue(pad, out defaultFlags);

                        if (enableVelocity && pad != MoonNote.DrumPad.Kick)
                        {
                            processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                            {
                                if (eventProcessParams.timedEvent.midiEvent is not NoteEvent noteEvent)
                                {
                                    YargLogger.FailFormat("Wrong note event type! Expected: {0}, Actual: {1}",
                                        typeof(NoteEvent), eventProcessParams.timedEvent.midiEvent.GetType());
                                    return;
                                }

                                var flags = defaultFlags;
                                switch (noteEvent.Velocity)
                                {
                                    case MidIOHelper.VELOCITY_ACCENT:
                                        flags |= MoonNote.Flags.ProDrums_Accent;
                                        break;
                                    case MidIOHelper.VELOCITY_GHOST:
                                        flags |= MoonNote.Flags.ProDrums_Ghost;
                                        break;
                                    default:
                                        break;
                                }

                                ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, fret, flags);
                            });
                        }
                        else
                        {
                            processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                            {
                                ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, fret, defaultFlags);
                            });
                        }

                        // Double-kick
                        if (pad == MoonNote.DrumPad.Kick)
                        {
                            processFnDict.Add(key - 1, (ref EventProcessParams eventProcessParams) => {
                                ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, fret, MoonNote.Flags.InstrumentPlus);
                            });
                        }
                    }
                }
            };

            foreach (var keyVal in MidIOHelper.PAD_TO_CYMBAL_LOOKUP)
            {
                int pad = (int)keyVal.Key;
                int midiKey = keyVal.Value;

                processFnDict.Add(midiKey, (ref EventProcessParams eventProcessParams) =>
                {
                    ProcessNoteOnEventAsFlagToggle(ref eventProcessParams, MoonNote.Flags.ProDrums_Cymbal, pad);
                });
            }

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildVocalsNoteProcessDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.RANGE_SHIFT_NOTE, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Vocals_RangeShift)
                },
                { MidIOHelper.LYRIC_SHIFT_NOTE, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Vocals_LyricShift)
                },

                { MidIOHelper.LYRICS_PHRASE_1, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Versus_Player1);
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Vocals_LyricPhrase);
                }},
                { MidIOHelper.LYRICS_PHRASE_2, (ref EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Versus_Player2);
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams, MoonPhrase.Type.Vocals_LyricPhrase);
                }},

                { MidIOHelper.PERCUSSION_NOTE, (ref EventProcessParams eventProcessParams) => {
                    foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
                    {
                        // Force percussion notes to be 0-length
                        var newParams = eventProcessParams;
                        newParams.timedEvent.endTick = newParams.timedEvent.startTick;
                        ProcessNoteOnEventAsNote(ref newParams, difficulty, 0, MoonNote.Flags.Vocals_Percussion,
                            sustainCutoff: false);
                    };
                }},
            };

            for (int i = MidIOHelper.VOCALS_RANGE_START; i <= MidIOHelper.VOCALS_RANGE_END; i++)
            {
                int rawNote = i; // Capture the note value
                processFnDict.Add(i, (ref EventProcessParams eventProcessParams) => {
                    foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
                    {
                        ProcessNoteOnEventAsNote(ref eventProcessParams, difficulty, rawNote, sustainCutoff: false);
                    };
                });
            }

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildProKeysNoteProcessDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.PRO_KEYS_SHIFT_0, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift0, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.PRO_KEYS_SHIFT_1, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift1, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.PRO_KEYS_SHIFT_2, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift2, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.PRO_KEYS_SHIFT_3, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift3, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.PRO_KEYS_SHIFT_4, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift4, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.PRO_KEYS_SHIFT_5, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_RangeShift5, eventProcessParams.trackDifficulty)
                },

                { MidIOHelper.PRO_KEYS_GLISSANDO, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.ProKeys_Glissando, eventProcessParams.trackDifficulty)
                },
                { MidIOHelper.TRILL_LANE_NOTE, (ref EventProcessParams eventProcessParams) =>
                    ProcessNoteOnEventAsSpecialPhrase(ref eventProcessParams,
                        MoonPhrase.Type.TrillLane, eventProcessParams.trackDifficulty)
                },
            };

            for (int key = MidIOHelper.PRO_KEYS_RANGE_START; key <= MidIOHelper.PRO_KEYS_RANGE_END; key++)
            {
                int fret = key - MidIOHelper.PRO_KEYS_RANGE_START;

                processFnDict.Add(key, (ref EventProcessParams eventProcessParams) =>
                {
                    if (eventProcessParams.trackDifficulty is null)
                    {
                        YargLogger.Fail("`trackDifficulty` cannot be null when processing Pro Keys!");
                        return;
                    }

                    var diff = eventProcessParams.trackDifficulty.Value;
                    ProcessNoteOnEventAsNote(ref eventProcessParams, diff, fret);
                });
            }

            return processFnDict;
        }
    }
}