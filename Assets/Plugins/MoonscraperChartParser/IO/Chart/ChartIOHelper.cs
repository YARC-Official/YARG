// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace MoonscraperChartEditor.Song.IO
{
    public static class ChartIOHelper
    {
        public enum FileSubType
        {
            Default,

            // Stores space characters found in ChartEvent objects as Japanese full-width spaces. Need to convert this back when loading.
            MoonscraperPropriety,
        }

        public const string
            c_dataBlockSong = "[Song]"
            , c_dataBlockSyncTrack = "[SyncTrack]"
            , c_dataBlockEvents = "[Events]"
            ;

        public const int c_proDrumsOffset = 64;
        public const int c_instrumentPlusOffset = 32;
        public const int c_drumsAccentOffset = 33;
        public const int c_drumsGhostOffset = 39;

        public const int c_starpowerId = 2;
        public const int c_starpowerDrumFillId = 64;
        public const int c_drumRollStandardId = 65;
        public const int c_drumRollSpecialId = 66;

        public enum TrackLoadType
        {
            Guitar,
            Drums,
            GHLiveGuitar,

            Unrecognised
        }

        public static readonly IReadOnlyDictionary<int, int> c_guitarNoteNumLookup = new Dictionary<int, int>()
        {
            { 0, (int)MoonNote.GuitarFret.Green     },
            { 1, (int)MoonNote.GuitarFret.Red       },
            { 2, (int)MoonNote.GuitarFret.Yellow    },
            { 3, (int)MoonNote.GuitarFret.Blue      },
            { 4, (int)MoonNote.GuitarFret.Orange    },
            { 7, (int)MoonNote.GuitarFret.Open      },
        };

        public static readonly IReadOnlyDictionary<int, MoonNote.Flags> c_guitarFlagNumLookup = new Dictionary<int, MoonNote.Flags>()
        {
            { 5      , MoonNote.Flags.Forced },
            { 6      , MoonNote.Flags.Tap },
        };

        public static readonly IReadOnlyDictionary<int, int> c_drumNoteNumLookup = new Dictionary<int, int>()
        {
            { 0, (int)MoonNote.DrumPad.Kick      },
            { 1, (int)MoonNote.DrumPad.Red       },
            { 2, (int)MoonNote.DrumPad.Yellow    },
            { 3, (int)MoonNote.DrumPad.Blue      },
            { 4, (int)MoonNote.DrumPad.Orange    },
            { 5, (int)MoonNote.DrumPad.Green     },
        };

        // Default flags for drums notes
        public static readonly IReadOnlyDictionary<int, MoonNote.Flags> c_drumNoteDefaultFlagsLookup = new Dictionary<int, MoonNote.Flags>()
        {
            { (int)MoonNote.DrumPad.Kick      , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Red       , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Yellow    , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Blue      , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Orange    , MoonNote.Flags.None },   // Orange becomes green during 4-lane
            { (int)MoonNote.DrumPad.Green     , MoonNote.Flags.None },
        };

        public static readonly IReadOnlyDictionary<int, int> c_ghlNoteNumLookup = new Dictionary<int, int>()
        {
            { 0, (int)MoonNote.GHLiveGuitarFret.White1    },
            { 1, (int)MoonNote.GHLiveGuitarFret.White2    },
            { 2, (int)MoonNote.GHLiveGuitarFret.White3    },
            { 3, (int)MoonNote.GHLiveGuitarFret.Black1    },
            { 4, (int)MoonNote.GHLiveGuitarFret.Black2    },
            { 8, (int)MoonNote.GHLiveGuitarFret.Black3    },
            { 7, (int)MoonNote.GHLiveGuitarFret.Open      },
        };

        public static readonly IReadOnlyDictionary<int, MoonNote.Flags> c_ghlFlagNumLookup = c_guitarFlagNumLookup;

        public static readonly IReadOnlyDictionary<string, MoonSong.Difficulty> c_trackNameToTrackDifficultyLookup = new Dictionary<string, MoonSong.Difficulty>()
        {
            { "Easy",   MoonSong.Difficulty.Easy    },
            { "Medium", MoonSong.Difficulty.Medium  },
            { "Hard",   MoonSong.Difficulty.Hard    },
            { "Expert", MoonSong.Difficulty.Expert  },
        };

        public static readonly IReadOnlyDictionary<string, MoonSong.MoonInstrument> c_instrumentStrToEnumLookup = new Dictionary<string, MoonSong.MoonInstrument>()
        {
            { "Single",         MoonSong.MoonInstrument.Guitar },
            { "DoubleGuitar",   MoonSong.MoonInstrument.GuitarCoop },
            { "DoubleBass",     MoonSong.MoonInstrument.Bass },
            { "DoubleRhythm",   MoonSong.MoonInstrument.Rhythm },
            { "Drums",          MoonSong.MoonInstrument.Drums },
            { "Keyboard",       MoonSong.MoonInstrument.Keys },
            { "GHLGuitar",      MoonSong.MoonInstrument.GHLiveGuitar },
            { "GHLBass",        MoonSong.MoonInstrument.GHLiveBass },
            { "GHLRhythm",      MoonSong.MoonInstrument.GHLiveRhythm },
            { "GHLCoop",        MoonSong.MoonInstrument.GHLiveCoop },
        };

        public static readonly IReadOnlyDictionary<MoonSong.MoonInstrument, TrackLoadType> c_instrumentParsingTypeLookup = new Dictionary<MoonSong.MoonInstrument, TrackLoadType>()
        {
            // Other instruments default to loading as a guitar type track
            { MoonSong.MoonInstrument.Drums, TrackLoadType.Drums },
            { MoonSong.MoonInstrument.GHLiveGuitar, TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveBass,  TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveRhythm,  TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveCoop,  TrackLoadType.GHLiveGuitar },
        };

        public static class MetaData
        {
            const string QUOTEVALIDATE = @"""[^""\\]*(?:\\.[^""\\]*)*""";
            const string QUOTESEARCH = "\"([^\"]*)\"";
            const string FLOATSEARCH = @"[\-\+]?\d+(\.\d+)?";       // US culture only

            public static readonly System.Globalization.CultureInfo c_cultureInfo = new System.Globalization.CultureInfo("en-US");

            public enum MetadataValueType
            {
                String,
                Float,
                Player2,
                Difficulty,
                Year,
            }

            public class MetadataItem
            {
                string m_key;
                Regex m_readerParseRegex;
                string m_saveFormat;

                static readonly string c_metaDataSaveFormat = string.Format("{0}{{0}} = \"{{{{0}}}}\"{1}", Globals.TABSPACE, Globals.LINE_ENDING);
                static readonly string c_metaDataSaveFormatNoQuote = string.Format("{0}{{0}} = {{{{0}}}}{1}", Globals.TABSPACE, Globals.LINE_ENDING);

                public string key { get { return m_key; } }
                public Regex regex { get { return m_readerParseRegex; } }
                public string saveFormat { get { return m_saveFormat; } }

                public MetadataItem(string key, MetadataValueType type)
                {
                    m_key = key;

                    Regex parseStrRegex = new Regex(key + " = " + QUOTEVALIDATE, RegexOptions.Compiled);

                    switch (type)
                    {
                        case MetadataValueType.String:
                            {
                                m_readerParseRegex = parseStrRegex;
                                m_saveFormat = string.Format(c_metaDataSaveFormat, key);
                                break;
                            }

                        case MetadataValueType.Float:
                            {
                                m_readerParseRegex = new Regex(key + " = " + FLOATSEARCH, RegexOptions.Compiled);
                                m_saveFormat = string.Format(c_cultureInfo, c_metaDataSaveFormatNoQuote, key);
                                break;
                            }

                        case MetadataValueType.Player2:
                            {
                                m_readerParseRegex = new Regex(key + @" = \w+", RegexOptions.Compiled);
                                m_saveFormat = string.Format(c_metaDataSaveFormatNoQuote, key);
                                break;
                            }

                        case MetadataValueType.Difficulty:
                            {
                                m_readerParseRegex = new Regex(key + @" = \d+", RegexOptions.Compiled);
                                m_saveFormat = string.Format(c_metaDataSaveFormatNoQuote, key);
                                break;
                            }

                        case MetadataValueType.Year:
                            {
                                m_readerParseRegex = parseStrRegex;
                                m_saveFormat = string.Format("{0}{1} = \", {{0}}\"{2}", Globals.TABSPACE, "Year", Globals.LINE_ENDING);
                                break;
                            }

                        default:
                            throw new System.Exception("Unhandled Metadata item type");
                    }
                }
            }

            public readonly static MetadataItem name = new MetadataItem("Name", MetadataValueType.String);
            public readonly static MetadataItem artist = new MetadataItem("Artist", MetadataValueType.String);
            public readonly static MetadataItem charter = new MetadataItem("Charter", MetadataValueType.String);
            public readonly static MetadataItem offset = new MetadataItem("Offset", MetadataValueType.Float);
            public readonly static MetadataItem resolution = new MetadataItem("Resolution", MetadataValueType.Float);
            public readonly static MetadataItem player2 = new MetadataItem("Player2", MetadataValueType.Player2);
            public readonly static MetadataItem difficulty = new MetadataItem("Difficulty", MetadataValueType.Difficulty);
            public readonly static MetadataItem length = new MetadataItem("Length", MetadataValueType.Float);
            public readonly static MetadataItem previewStart = new MetadataItem("PreviewStart", MetadataValueType.Float);
            public readonly static MetadataItem previewEnd = new MetadataItem("PreviewEnd", MetadataValueType.Float);
            public readonly static MetadataItem genre = new MetadataItem("Genre", MetadataValueType.String);
            public readonly static MetadataItem year = new MetadataItem("Year", MetadataValueType.Year);
            public readonly static MetadataItem album = new MetadataItem("Album", MetadataValueType.String);
            public readonly static MetadataItem mediaType = new MetadataItem("MediaType", MetadataValueType.String);
            public readonly static MetadataItem musicStream = new MetadataItem("MusicStream", MetadataValueType.String);
            public readonly static MetadataItem guitarStream = new MetadataItem("GuitarStream", MetadataValueType.String);
            public readonly static MetadataItem bassStream = new MetadataItem("BassStream", MetadataValueType.String);
            public readonly static MetadataItem rhythmStream = new MetadataItem("RhythmStream", MetadataValueType.String);
            public readonly static MetadataItem drumStream = new MetadataItem("DrumStream", MetadataValueType.String);
            public readonly static MetadataItem drum2Stream = new MetadataItem("Drum2Stream", MetadataValueType.String);
            public readonly static MetadataItem drum3Stream = new MetadataItem("Drum3Stream", MetadataValueType.String);
            public readonly static MetadataItem drum4Stream = new MetadataItem("Drum4Stream", MetadataValueType.String);
            public readonly static MetadataItem vocalStream = new MetadataItem("VocalStream", MetadataValueType.String);
            public readonly static MetadataItem keysStream = new MetadataItem("KeysStream", MetadataValueType.String);
            public readonly static MetadataItem crowdStream = new MetadataItem("CrowdStream", MetadataValueType.String);

            public static string ParseAsString(string line)
            {
                return Regex.Matches(line, QUOTESEARCH)[0].ToString().Trim('"');
            }

            public static float ParseAsFloat(string line)
            {
                return float.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString(), c_cultureInfo);  // .chart format only allows '.' as decimal seperators. Need to parse correctly under any locale.
            }

            public static short ParseAsShort(string line)
            {
                return short.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString());
            }
        }

        public class NoteFlagPriority
        {
            // Flags to skip adding if the corresponding flag is already present
            private static readonly IReadOnlyDictionary<MoonNote.Flags, MoonNote.Flags> c_noteBlockingFlagsLookup = new Dictionary<MoonNote.Flags, MoonNote.Flags>()
            {
                { MoonNote.Flags.Forced, MoonNote.Flags.Tap },
                { MoonNote.Flags.ProDrums_Ghost, MoonNote.Flags.ProDrums_Accent },
            };

            // Flags to remove if the corresponding flag is being added
            private static readonly IReadOnlyDictionary<MoonNote.Flags, MoonNote.Flags> c_noteFlagsToRemoveLookup = c_noteBlockingFlagsLookup.ToDictionary((i) => i.Value, (i) => i.Key);

            public static readonly NoteFlagPriority Forced = new NoteFlagPriority(MoonNote.Flags.Forced);
            public static readonly NoteFlagPriority Tap = new NoteFlagPriority(MoonNote.Flags.Tap);
            public static readonly NoteFlagPriority InstrumentPlus = new NoteFlagPriority(MoonNote.Flags.InstrumentPlus);
            public static readonly NoteFlagPriority Cymbal = new NoteFlagPriority(MoonNote.Flags.ProDrums_Cymbal);
            public static readonly NoteFlagPriority Accent = new NoteFlagPriority(MoonNote.Flags.ProDrums_Accent);
            public static readonly NoteFlagPriority Ghost = new NoteFlagPriority(MoonNote.Flags.ProDrums_Ghost);

            private static readonly IReadOnlyList<NoteFlagPriority> priorities = new List<NoteFlagPriority>()
            {
                Forced,
                Tap,
                InstrumentPlus,
                Cymbal,
                Accent,
                Ghost,
            };

            public MoonNote.Flags flagToAdd { get; } = MoonNote.Flags.None;
            public MoonNote.Flags blockingFlag { get; } = MoonNote.Flags.None;
            public MoonNote.Flags flagToRemove { get; } = MoonNote.Flags.None;

            public NoteFlagPriority(MoonNote.Flags flag)
            {
                flagToAdd = flag;

                MoonNote.Flags blockingFlag;
                if (c_noteBlockingFlagsLookup.TryGetValue(flagToAdd, out blockingFlag))
                {
                    this.blockingFlag = blockingFlag;
                }

                MoonNote.Flags flagToRemove;
                if (c_noteFlagsToRemoveLookup.TryGetValue(flagToAdd, out flagToRemove))
                {
                    this.flagToRemove = flagToRemove;
                }
            }

            public bool TryApplyToNote(MoonNote moonNote)
            {
                // Don't add if the flag to be added is lower-priority than a conflicting, already-added flag
                if (blockingFlag != MoonNote.Flags.None && moonNote.flags.HasFlag(blockingFlag))
                {
                    return false;
                }

                // Flag can be added without issue
                moonNote.flags |= flagToAdd;

                // Remove flags that are lower-priority than the added flag
                if (flagToRemove != MoonNote.Flags.None && moonNote.flags.HasFlag(flagToRemove))
                {
                    moonNote.flags &= ~flagToRemove;
                }

                return true;
            }

            public bool AreFlagsValid(MoonNote.Flags flags)
            {
                if (flagToAdd == MoonNote.Flags.None)
                {
                    // No flag to validate against
                    return true;
                }

                if (blockingFlag != MoonNote.Flags.None)
                {
                    if (flags.HasFlag(blockingFlag) && flags.HasFlag(flagToAdd))
                    {
                        // Note has conflicting flags
                        return false;
                    }
                }

                if (flagToRemove != MoonNote.Flags.None)
                {
                    if (flags.HasFlag(flagToAdd) && flags.HasFlag(flagToRemove))
                    {
                        // Note has conflicting flags
                        return false;
                    }
                }

                return true;
            }

            public static bool AreFlagsValidForAll(MoonNote.Flags flags, out NoteFlagPriority invalidPriority)
            {
                foreach (var priority in priorities)
                {
                    if (!priority.AreFlagsValid(flags))
                    {
                        invalidPriority = priority;
                        return false;
                    }
                }

                invalidPriority = null;
                return true;
            }
        }
    }
}
