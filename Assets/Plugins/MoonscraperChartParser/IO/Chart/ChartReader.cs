// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using MoonscraperEngine;

using NoteFlagPriority = MoonscraperChartEditor.Song.IO.ChartIOHelper.NoteFlagPriority;

namespace MoonscraperChartEditor.Song.IO
{
    public static class ChartReader
    {
        struct Anchor
        {
            public uint tick;
            public double anchorTime;
        }

        struct NoteFlag
        {
            public uint tick;
            public MoonNote.Flags flag;
            public int noteNumber;

            public NoteFlag(uint tick, MoonNote.Flags flag, int noteNumber)
            {
                this.tick = tick;
                this.flag = flag;
                this.noteNumber = noteNumber;
            }
        }

        struct NoteEvent
        {
            public uint tick;
            public int noteNumber;          
            public uint length;
        }

        struct NoteProcessParams
        {
            public MoonChart moonChart;
            public NoteEvent noteEvent;
            public List<NoteEventProcessFn> postNotesAddedProcessList;
        }

        delegate void NoteEventProcessFn(in NoteProcessParams noteProcessParams);

        // These dictionaries map the number of a note event to a specific function of how to process them
        static readonly IReadOnlyDictionary<int, NoteEventProcessFn> GuitarChartNoteNumberToProcessFnMap = new Dictionary<int, NoteEventProcessFn>()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Green); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Red); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Yellow); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Blue); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Orange); }},
            { 7, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Open); }},

            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Tap); }},
        };

        static readonly IReadOnlyDictionary<int, NoteEventProcessFn> DrumsChartNoteNumberToProcessFnMap = new Dictionary<int, NoteEventProcessFn>()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Kick); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Red); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Yellow); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Blue); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Orange); }},
            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Green); }},

            { ChartIOHelper.c_instrumentPlusOffset, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Kick, MoonNote.Flags.DoubleKick);
            } },

            { ChartIOHelper.c_proDrumsOffset + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.c_proDrumsOffset + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.c_proDrumsOffset + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Cymbal);
            } },

            // { ChartIOHelper.c_drumsAccentOffset + 0, ... }  // Reserved for kick accents, if they should ever be a thing
            { ChartIOHelper.c_drumsAccentOffset + 1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.c_drumsAccentOffset + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.c_drumsAccentOffset + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.c_drumsAccentOffset + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.c_drumsAccentOffset + 5, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Accent);
            } },

            // { ChartIOHelper.c_drumsGhostOffset + 0, ... }  // Reserved for kick ghosts, if they should ever be a thing
            { ChartIOHelper.c_drumsGhostOffset + 1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.c_drumsGhostOffset + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.c_drumsGhostOffset + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.c_drumsGhostOffset + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.c_drumsGhostOffset + 5, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Ghost);
            } },
        };

        static readonly IReadOnlyDictionary<int, NoteEventProcessFn> GhlChartNoteNumberToProcessFnMap = new Dictionary<int, NoteEventProcessFn>()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White1); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White2); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White3); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black1); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black2); }},
            { 8, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black3); }},
            { 7, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Open); }},

            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Tap); }},
        };

        public static MoonSong ReadChart(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                    throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);
                bool standardChartFormat = extension == ".chart";

                if (standardChartFormat || extension == MsceIOHelper.FileExtention)
                {
                    MoonSong moonSong = new MoonSong();

                    ChartIOHelper.FileSubType fileLoadType = standardChartFormat ? ChartIOHelper.FileSubType.Default : ChartIOHelper.FileSubType.MoonscraperPropriety;

                    LoadChart(moonSong, filepath, fileLoadType);

                    return moonSong;
                }
                else
                {
                    throw new Exception("Bad file type");
                }

            }
            catch (System.Exception e)
            {
                throw new Exception("Could not open file: " + e.Message);
            }
        }

        static void LoadChart(MoonSong moonSong, string filepath, ChartIOHelper.FileSubType fileLoadType)
        {
            bool open = false;
            string dataName = string.Empty;

            List<string> dataStrings = new List<string>();
#if TIMING_DEBUG
        float time = Time.realtimeSinceStartup;
#endif
            StreamReader sr = File.OpenText(filepath);

            // Gather lines between {} brackets and submit data
            while (!sr.EndOfStream)
            {
                string trimmedLine = sr.ReadLine().Trim();
                if (trimmedLine.Length <= 0)
                    continue;

                if (trimmedLine[0] == '[' && trimmedLine[trimmedLine.Length - 1] == ']')
                {
                    dataName = trimmedLine;
                }
                else if (trimmedLine == "{")
                {
                    open = true;
                }
                else if (trimmedLine == "}")
                {
                    open = false;

                    // Submit data
                    SubmitChartData(moonSong, dataName, dataStrings, fileLoadType, filepath);

                    dataName = string.Empty;
                    dataStrings.Clear();
                }
                else
                {
                    if (open)
                    {
                        // Add data into the array
                        dataStrings.Add(trimmedLine);
                    }
                    else if (dataStrings.Count > 0 && dataName != string.Empty)
                    {
                        // Submit data
                        SubmitChartData(moonSong, dataName, dataStrings, fileLoadType, filepath);

                        dataName = string.Empty;
                        dataStrings.Clear();
                    }
                }
            }

            sr.Close();

#if TIMING_DEBUG
        Debug.Log("Chart file load time: " + (Time.realtimeSinceStartup - time));
        time = Time.realtimeSinceStartup;
#endif

            moonSong.UpdateCache();
        }

        static void SubmitChartData(MoonSong moonSong, string dataName, List<string> stringData, ChartIOHelper.FileSubType fileLoadType, string filePath = "")
        {
            switch (dataName)
            {
                case ChartIOHelper.c_dataBlockSong:
#if SONG_DEBUG
                Debug.Log("Loading chart properties");
#endif
                    SubmitDataSong(moonSong, stringData, new FileInfo(filePath).Directory.FullName);
                    break;
                case ChartIOHelper.c_dataBlockSyncTrack:
#if SONG_DEBUG
                Debug.Log("Loading sync data");
#endif
                case ChartIOHelper.c_dataBlockEvents:
#if SONG_DEBUG
                Debug.Log("Loading events data");
#endif
                    SubmitDataGlobals(moonSong, stringData, fileLoadType);
                    break;
                default:
                    // Determine what difficulty
                    foreach (var kvPair in ChartIOHelper.c_trackNameToTrackDifficultyLookup)
                    {
                        if (Regex.IsMatch(dataName, string.Format(@"\[{0}.", kvPair.Key)))
                        {
                            MoonSong.Difficulty chartDiff = kvPair.Value;
                            int instumentStringOffset = 1 + kvPair.Key.Length;

                            string instrumentKey = dataName.Substring(instumentStringOffset, dataName.Length - instumentStringOffset - 1);
                            MoonSong.MoonInstrument moonInstrument;
                            if (ChartIOHelper.c_instrumentStrToEnumLookup.TryGetValue(instrumentKey, out moonInstrument))
                            {
                                ChartIOHelper.TrackLoadType instrumentParsingType;
                                if (!ChartIOHelper.c_instrumentParsingTypeLookup.TryGetValue(moonInstrument, out instrumentParsingType))
                                {
                                    instrumentParsingType = ChartIOHelper.TrackLoadType.Guitar;
                                }

                                LoadChart(moonSong.GetChart(moonInstrument, chartDiff), stringData, instrumentParsingType, fileLoadType);
                            }
                            else
                            {
                                LoadUnrecognisedChart(moonSong, dataName, stringData, fileLoadType);
                            }

                            goto OnChartLoaded;
                        }
                    }

                    {
                        // Add to the unused chart list
                        LoadUnrecognisedChart(moonSong, dataName, stringData, fileLoadType);
                        goto OnChartLoaded;
                    }

                // Easy break out of loop
                OnChartLoaded:
                    return;
            }
        }

        static void LoadUnrecognisedChart(MoonSong moonSong, string dataName, List<string> stringData, ChartIOHelper.FileSubType fileLoadType)
        {
            dataName = dataName.TrimStart('[');
            dataName = dataName.TrimEnd(']');
            MoonChart unrecognisedMoonChart = new MoonChart(moonSong, MoonSong.MoonInstrument.Unrecognised, dataName);
            LoadChart(unrecognisedMoonChart, stringData, ChartIOHelper.TrackLoadType.Unrecognised, fileLoadType);
            moonSong.unrecognisedCharts.Add(unrecognisedMoonChart);
        }

        static void SubmitDataSong(MoonSong moonSong, List<string> stringData, string audioDirectory = "")
        {
#if SONG_DEBUG
        Debug.Log("Loading song properties");
#endif
#if TIMING_DEBUG
        float time = Time.realtimeSinceStartup;
#endif

            Metadata metaData = moonSong.metaData;

            try
            {
                foreach (string line in stringData)
                {
                    // Name = "5000 Robots"
                    if (ChartIOHelper.MetaData.name.regex.IsMatch(line))
                    {
                        metaData.name = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    // Artist = "TheEruptionOffer"
                    else if (ChartIOHelper.MetaData.artist.regex.IsMatch(line))
                    {
                        metaData.artist = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    // Charter = "TheEruptionOffer"
                    else if (ChartIOHelper.MetaData.charter.regex.IsMatch(line))
                    {
                        metaData.charter = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    // Album = "Rockman Holic"
                    else if (ChartIOHelper.MetaData.album.regex.IsMatch(line))
                    {
                        metaData.album = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    // Offset = 0
                    else if (ChartIOHelper.MetaData.offset.regex.IsMatch(line))
                    {
                        moonSong.offset = ChartIOHelper.MetaData.ParseAsFloat(line);
                    }

                    // Resolution = 192
                    else if (ChartIOHelper.MetaData.resolution.regex.IsMatch(line))
                    {
                        moonSong.resolution = ChartIOHelper.MetaData.ParseAsShort(line);
                    }

                    // Player2 = bass
                    else if (ChartIOHelper.MetaData.player2.regex.IsMatch(line))
                    {
                        string[] instrumentTypes = { "Bass", "Rhythm" };
                        string split = line.Split('=')[1].Trim();

                        foreach (string instrument in instrumentTypes)
                        {
                            if (split.Equals(instrument, System.StringComparison.InvariantCultureIgnoreCase))
                            {
                                metaData.player2 = instrument;
                                break;
                            }
                        }
                    }

                    // Difficulty = 0
                    else if (ChartIOHelper.MetaData.difficulty.regex.IsMatch(line))
                    {
                        metaData.difficulty = int.Parse(Regex.Matches(line, @"\d+")[0].ToString());
                    }

                    // Length = 300
                    else if (ChartIOHelper.MetaData.length.regex.IsMatch(line))
                    {
                        moonSong.manualLength = ChartIOHelper.MetaData.ParseAsFloat(line);
                    }

                    // PreviewStart = 0.00
                    else if (ChartIOHelper.MetaData.previewStart.regex.IsMatch(line))
                    {
                        metaData.previewStart = ChartIOHelper.MetaData.ParseAsFloat(line);
                    }

                    // PreviewEnd = 0.00
                    else if (ChartIOHelper.MetaData.previewEnd.regex.IsMatch(line))
                    {
                        metaData.previewEnd = ChartIOHelper.MetaData.ParseAsFloat(line);
                    }

                    // Genre = "rock"
                    else if (ChartIOHelper.MetaData.genre.regex.IsMatch(line))
                    {
                        metaData.genre = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    // MediaType = "cd"
                    else if (ChartIOHelper.MetaData.mediaType.regex.IsMatch(line))
                    {
                        metaData.mediatype = ChartIOHelper.MetaData.ParseAsString(line);
                    }

                    else if (ChartIOHelper.MetaData.year.regex.IsMatch(line))
                        metaData.year = Regex.Replace(ChartIOHelper.MetaData.ParseAsString(line), @"\D", "");

                    // MusicStream = "ENDLESS REBIRTH.ogg"
                    else if (ChartIOHelper.MetaData.musicStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Song, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.guitarStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Guitar, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.bassStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Bass, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.rhythmStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Rhythm, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.drumStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Drum, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.drum2Stream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Drums_2, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.drum3Stream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Drums_3, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.drum4Stream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Drums_4, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.vocalStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Vocals, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.keysStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Keys, line, audioDirectory);
                    }
                    else if (ChartIOHelper.MetaData.crowdStream.regex.IsMatch(line))
                    {
                        AudioLoadFromChart(moonSong, MoonSong.AudioInstrument.Crowd, line, audioDirectory);
                    }
                }

#if TIMING_DEBUG
            Debug.Log("Song properties load time: " + (Time.realtimeSinceStartup - time));
#endif
            }
            catch (System.Exception e)
            {
                Debug.Log($"Error when reading chart metadata: {e.Message}");
            }
        }

        static void AudioLoadFromChart(MoonSong moonSong, MoonSong.AudioInstrument streamAudio, string line, string audioDirectory)
        {
            string audioFilepath = ChartIOHelper.MetaData.ParseAsString(line);

            // Check if it's already the full path. If not, make it relative to the chart file.
            if (!Path.IsPathRooted(audioFilepath))
                audioFilepath = Path.Combine(audioDirectory, audioFilepath);

            if (File.Exists(audioFilepath) && Utility.validateExtension(audioFilepath, Globals.validAudioExtensions))
                moonSong.SetAudioLocation(streamAudio, Path.GetFullPath(audioFilepath));
        }

        static void SubmitDataGlobals(MoonSong moonSong, List<string> stringData, ChartIOHelper.FileSubType fileLoadType)
        {
            const int TEXT_POS_TICK = 0;
            const int TEXT_POS_EVENT_TYPE = 2;
            const int TEXT_POS_DATA_1 = 3;

#if TIMING_DEBUG
        float time = Time.realtimeSinceStartup;
#endif

            List<Anchor> anchorData = new List<Anchor>();

            foreach (string line in stringData)
            {
                string[] stringSplit = line.Split(' ');
                uint tick;
                string eventType;
                if (stringSplit.Length > TEXT_POS_DATA_1 && uint.TryParse(stringSplit[TEXT_POS_TICK], out tick))
                {
                    eventType = stringSplit[TEXT_POS_EVENT_TYPE];
                    eventType = eventType.ToLower();
                }
                else
                    continue;

                switch (eventType)
                {
                    case ("ts"):
                        uint numerator;
                        uint denominator = 2;

                        if (!uint.TryParse(stringSplit[TEXT_POS_DATA_1], out numerator))
                            continue;

                        if (stringSplit.Length > TEXT_POS_DATA_1 + 1 && !uint.TryParse(stringSplit[TEXT_POS_DATA_1 + 1], out denominator))
                            continue;

                        moonSong.Add(new TimeSignature(tick, numerator, (uint)(Mathf.Pow(2, denominator))), false);
                        break;
                    case ("b"):
                        uint value;
                        if (!uint.TryParse(stringSplit[TEXT_POS_DATA_1], out value))
                            continue;

                        moonSong.Add(new BPM(tick, value), false);
                        break;
                    case ("e"):
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        int startIndex = TEXT_POS_DATA_1;
                        bool isSection = false;

                        if (stringSplit.Length > TEXT_POS_DATA_1 + 1 && stringSplit[TEXT_POS_DATA_1] == "\"section")
                        {
                            startIndex = TEXT_POS_DATA_1 + 1;
                            isSection = true;
                        }

                        for (int i = startIndex; i < stringSplit.Length; ++i)
                        {
                            sb.Append(stringSplit[i].Trim('"'));
                            if (i < stringSplit.Length - 1)
                                sb.Append(" ");
                        }

                        if (isSection)
                        {
                            moonSong.Add(new Section(sb.ToString(), tick), false);
                        }
                        else
                        {
                            string eventTitle = sb.ToString();

                            if (LyricHelper.IsLyric(eventTitle) && fileLoadType == ChartIOHelper.FileSubType.MoonscraperPropriety)
                            {
                                foreach (var replacement in MsceIOHelper.LyricEventCharReplacementFromMsce)
                                {
                                    eventTitle = eventTitle.Replace(replacement.Key, replacement.Value);
                                }
                            }

                            moonSong.Add(new Event(eventTitle, tick), false);
                        }

                        break;
                    case ("a"):
                        ulong anchorValue;
                        if (ulong.TryParse(stringSplit[TEXT_POS_DATA_1], out anchorValue))
                        {
                            Anchor a;
                            a.tick = tick;
                            a.anchorTime = (float)(anchorValue / 1000000.0d);
                            anchorData.Add(a);
                        }
                        break;
                    default:
                        break;
                }
            }

            BPM[] bpms = moonSong.syncTrack.OfType<BPM>().ToArray();        // BPMs are currently uncached
            foreach (Anchor anchor in anchorData)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(anchor.tick, bpms);
                if (bpms[arrayPos].tick == anchor.tick)
                {
                    bpms[arrayPos].anchor = anchor.anchorTime;
                }
                else
                {
                    // Create a new anchored bpm
                    uint value;
                    if (bpms[arrayPos].tick > anchor.tick)
                        value = bpms[arrayPos - 1].value;
                    else
                        value = bpms[arrayPos].value;

                    BPM anchoredBPM = new BPM(anchor.tick, value);
                    anchoredBPM.anchor = anchor.anchorTime;
                }
            }
#if TIMING_DEBUG
        Debug.Log("Synctrack load time: " + (Time.realtimeSinceStartup - time));
#endif
        }

        /*************************************************************************************
            Chart Loading
        **************************************************************************************/

        static int FastStringToIntParse(string str, int index, int length)
        {
            // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
            int y = 0;
            for (int i = index; i < index + length; i++)
                y = y * 10 + (str[i] - '0');

            return y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AdvanceNextWord(string line, ref int startIndex, ref int length)
        {
            length = 0;
            while (startIndex < line.Length && line[startIndex] == ' ') { ++startIndex; };
            while ((startIndex + ++length) < line.Length && line[startIndex + length] != ' ') ;
        }

        static void LoadChart(MoonChart moonChart, IList<string> data, ChartIOHelper.TrackLoadType instrument, ChartIOHelper.FileSubType fileLoadType)
        {
#if TIMING_DEBUG
        float time = Time.realtimeSinceStartup;
#endif
            List<NoteFlag> flags = new List<NoteFlag>();
            List<NoteEventProcessFn> postNotesAddedProcessList = new List<NoteEventProcessFn>();

            NoteProcessParams processParams = new NoteProcessParams()
            {
                moonChart = moonChart,
                postNotesAddedProcessList = postNotesAddedProcessList
            };

            moonChart.SetCapacity(data.Count);

            var noteProcessDict = GetNoteProcessDict(moonChart.gameMode);

            try
            {
                // Load notes, collect flags
                foreach (string line in data)
                {
                    try
                    {
                        int stringStartIndex = 0;
                        int stringLength = 0;

                        // Advance to tick
                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                        uint tick = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                        // Advance to equality
                        {
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                        }

                        // Advance to type
                        {
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                        }

                        switch (line[stringStartIndex])    // Note this will need to be changed if keys are ever greater than 1 character long
                        {
                            case ('N'):
                            case ('n'):
                                {
                                    // Advance to note number
                                    {
                                        stringStartIndex += stringLength;
                                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                    }
                                    int fret_type = FastStringToIntParse(line, stringStartIndex, stringLength);

                                    // Advance to note length
                                    {
                                        stringStartIndex += stringLength;
                                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                    }
                                    uint length = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                                    if (instrument == ChartIOHelper.TrackLoadType.Unrecognised)
                                    {
                                        MoonNote newMoonNote = new MoonNote(tick, fret_type, length);
                                        moonChart.Add(newMoonNote, false);
                                    }
                                    else
                                    {
                                        NoteEventProcessFn processFn;
                                        if (noteProcessDict.TryGetValue(fret_type, out processFn))
                                        {
                                            NoteEvent noteEvent = new NoteEvent() { tick = tick, noteNumber = fret_type, length = length };
                                            processParams.noteEvent = noteEvent;
                                            processFn(processParams);
                                        }
                                    }

                                    break;
                                }

                            case ('S'):
                            case ('s'):
                                {
                                    // Advance to note number
                                    {
                                        stringStartIndex += stringLength;
                                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                    }

                                    int fret_type = FastStringToIntParse(line, stringStartIndex, stringLength);

                                    // Advance to note length
                                    {
                                        stringStartIndex += stringLength;
                                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                    }

                                    uint length = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                                    switch (fret_type)
                                    {
                                        case ChartIOHelper.c_starpowerId:
                                            {
                                                moonChart.Add(new Starpower(tick, length), false);
                                                break;
                                            }

                                        case ChartIOHelper.c_starpowerDrumFillId:
                                            {
                                                if (instrument == ChartIOHelper.TrackLoadType.Drums)
                                                {
                                                    moonChart.Add(new Starpower(tick, length, Starpower.Flags.ProDrums_Activation), false);
                                                }
                                                else
                                                {
                                                    Debug.Assert(false, "Found drum fill flag on incompatible instrument.");
                                                }
                                                break;
                                            }

                                        case ChartIOHelper.c_drumRollStandardId:
                                            {
                                                if (instrument == ChartIOHelper.TrackLoadType.Drums)
                                                {
                                                    moonChart.Add(new DrumRoll(tick, length, DrumRoll.Type.Standard), false);
                                                }
                                                else
                                                {
                                                    Debug.Assert(false, "Found standard drum roll flag on incompatible instrument.");
                                                }
                                                break;
                                            }
                                        case ChartIOHelper.c_drumRollSpecialId:
                                            {
                                                if (instrument == ChartIOHelper.TrackLoadType.Drums)
                                                {
                                                    moonChart.Add(new DrumRoll(tick, length, DrumRoll.Type.Special), false);
                                                }
                                                else
                                                {
                                                    Debug.Assert(false, "Found special drum roll flag on incompatible instrument.");
                                                }
                                                break;
                                            }

                                        default:
                                            continue;
                                    }

                                    break;
                                }
                            case ('E'):
                            case ('e'):
                                {
                                    // Advance to event
                                    {
                                        stringStartIndex += stringLength;
                                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                    }
                                    string eventName = line.Substring(stringStartIndex, stringLength);

                                    if (fileLoadType == ChartIOHelper.FileSubType.MoonscraperPropriety)
                                    {
                                        foreach (var replacement in MsceIOHelper.LocalEventCharReplacementFromMsce)
                                        {
                                            eventName = eventName.Replace(replacement.Key, replacement.Value);
                                        }
                                    }

                                    moonChart.Add(new ChartEvent(tick, eventName), false);
                                    break;
                                }
                            default:
                                break;
                        }

                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("Error parsing chart reader line \"" + line);
                    }
                }
                moonChart.UpdateCache();

                foreach (var fn in postNotesAddedProcessList)
                {
                    fn(processParams);
                }

#if TIMING_DEBUG
            Debug.Log("Chart load time: " + (Time.realtimeSinceStartup - time));
#endif
            }
            catch (System.Exception e)
            {
                // Bad load, most likely a parsing error
                Debug.LogError("Error parsing chart reader chart data");
                moonChart.Clear();
            }
        }

        static IReadOnlyDictionary<int, NoteEventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            switch (gameMode)
            {
                case MoonChart.GameMode.GHLGuitar:
                    {
                        return GhlChartNoteNumberToProcessFnMap;
                    }
                case MoonChart.GameMode.Drums:
                    {
                        return DrumsChartNoteNumberToProcessFnMap;
                    }

                default: break;
            }

            return GuitarChartNoteNumberToProcessFnMap;
        }

        static void ProcessNoteOnEventAsNote(in NoteProcessParams noteProcessParams, int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            MoonChart moonChart = noteProcessParams.moonChart;

            NoteEvent noteEvent = noteProcessParams.noteEvent;
            var tick = noteEvent.tick;
            var sus = noteEvent.length;

            MoonNote newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            moonChart.Add(newMoonNote, false);
        }

        static void ProcessNoteOnEventAsChordFlag(in NoteProcessParams noteProcessParams, NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((in NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsChordFlagPostDelay(processParams, flagEvent, flagData);
            });
        }

        static void ProcessNoteOnEventAsChordFlagPostDelay(in NoteProcessParams noteProcessParams, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            MoonChart moonChart = noteProcessParams.moonChart;

            int index, length;
            SongObjectHelper.FindObjectsAtPosition(noteEvent.tick, moonChart.notes, out index, out length);
            if (length > 0)
            {
                GroupAddFlags(moonChart.notes, flagData, index, length);
            }
        }

        static void ProcessNoteOnEventAsNoteFlagToggle(in NoteProcessParams noteProcessParams, int rawNote, NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((in NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsNoteFlagTogglePostDelay(processParams, rawNote, flagEvent, flagData);
            });
        }

        static void ProcessNoteOnEventAsNoteFlagTogglePostDelay(in NoteProcessParams noteProcessParams, int rawNote, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            MoonChart moonChart = noteProcessParams.moonChart;

            int index, length;
            SongObjectHelper.FindObjectsAtPosition(noteEvent.tick, moonChart.notes, out index, out length);
            if (length > 0)
            {
                for (int i = index; i < index + length; ++i)
                {
                    MoonNote moonNote = moonChart.notes[i];
                    if (moonNote.rawNote == rawNote)
                    {
                        TryAddNoteFlags(moonNote, flagData);
                    }
                }
            }
        }

        static void GroupAddFlags(IList<MoonNote> notes, NoteFlagPriority flagData, int index, int length)
        {
            for (int i = index; i < index + length; ++i)
            {
                TryAddNoteFlags(notes[i], flagData);
            }
        }

        static void TryAddNoteFlags(MoonNote moonNote, NoteFlagPriority flagData)
        {
            if (!flagData.TryApplyToNote(moonNote))
            {
                Debug.LogWarning($"Could not apply flag {flagData.flagToAdd} to a note. It was blocked by existing flag {flagData.blockingFlag}.");
            }
        }
    }
}
