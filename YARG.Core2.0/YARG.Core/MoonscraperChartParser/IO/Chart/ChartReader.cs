// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Core.Utility;

namespace MoonscraperChartEditor.Song.IO
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal static partial class ChartReader
    {
        private struct NoteFlag
        {
            public uint           tick;
            public MoonNote.Flags flag;
            public int            noteNumber;

            public NoteFlag(uint tick, MoonNote.Flags flag, int noteNumber)
            {
                this.tick = tick;
                this.flag = flag;
                this.noteNumber = noteNumber;
            }
        }

        private struct NoteEvent
        {
            public uint tick;
            public int  noteNumber;
            public uint length;
        }

        private struct NoteProcessParams
        {
            public MoonChart                chart;
            public ParseSettings            settings;
            public NoteEvent                noteEvent;
            public List<NoteEventProcessFn> postNotesAddedProcessList;
        }

        #region Utility

        // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastInt32Parse(ReadOnlySpan<char> text)
        {
            int value = 0;
            foreach (char character in text) value = value * 10 + (character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FastUint64Parse(ReadOnlySpan<char> text)
        {
            ulong value = 0;
            foreach (char character in text) value = value * 10 + (ulong) (character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char>
            GetNextWord(this ReadOnlySpan<char> buffer, out ReadOnlySpan<char> remaining) =>
            buffer.SplitOnceTrimmed(' ', out remaining);

        #endregion

        public static MoonSong ReadFromFile(string filepath)
        {
            var settings = ParseSettings.Default_Chart;
            return ReadFromFile(ref settings, filepath);
        }

        public static MoonSong ReadFromText(ReadOnlySpan<char> chartText)
        {
            var settings = ParseSettings.Default_Chart;
            return ReadFromText(ref settings, chartText);
        }

        public static MoonSong ReadFromFile(ref ParseSettings settings, string filepath)
        {
            try
            {
                if (!File.Exists(filepath)) throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".chart") throw new Exception("Bad file type");

                string text = File.ReadAllText(filepath);
                return ReadFromText(ref settings, text);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        private const uint DEFAULT_RESOLUTION = 192;
        public static MoonSong ReadFromText(ref ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            int textIndex = 0;

            static void ExpectSection(ReadOnlySpan<char> chartText, ref int textIndex,
                string name, out AsciiTrimSplitter sectionBody)
            {
                if (!GetNextSection(chartText, ref textIndex, out var sectionName, out sectionBody))
                    throw new InvalidDataException($"Required section [{name}] is missing!");

                if (!sectionName.Equals(name, StringComparison.Ordinal))
                    throw new InvalidDataException($"Invalid section ordering! Expected [{name}], found [{sectionName.ToString()}]");
            }

            // Check for the [Song] section first explicitly, need the Resolution property up-front
            ExpectSection(chartText, ref textIndex, ChartIOHelper.SECTION_SONG, out var sectionBody);
            var song = SubmitDataSong(sectionBody);

            // With a 192 resolution, .chart has a HOPO threshold of 65 ticks, not 64,
            // so we need to scale this factor to different resolutions (480 res = 162.5 threshold)
            // This extra tick is meant for some slight leniency; .mid has it too, but it's applied
            // after factoring in the resolution there, not before.
            const uint THRESHOLD_AT_DEFAULT = 65;
            song.hopoThreshold = settings.HopoThreshold > ParseSettings.SETTING_DEFAULT
                ? (uint) settings.HopoThreshold
                : (song.resolution * THRESHOLD_AT_DEFAULT) / DEFAULT_RESOLUTION;

            // Check for [SyncTrack] next, we need it for time conversions
            ExpectSection(chartText, ref textIndex, ChartIOHelper.SECTION_SYNC_TRACK, out sectionBody);
            SubmitDataSync(song, sectionBody);

            // Parse instrument tracks
            while (GetNextSection(chartText, ref textIndex, out var sectionName, out sectionBody))
            {
                SubmitChartData(ref settings, song, sectionName, sectionBody);
            }

            return song;
        }

        private static bool GetNextSection(ReadOnlySpan<char> chartText, ref int index,
            out ReadOnlySpan<char> sectionName, out AsciiTrimSplitter sectionBody)
        {
            static int GetLineCount(ReadOnlySpan<char> chartText, int startIndex, int relativeIndex)
            {
                var searchSpace = chartText[..(startIndex + relativeIndex)];

                int count = 0;
                int index;
                while ((index = searchSpace.IndexOf('\n')) >= 0)
                {
                    count++;
                    searchSpace = searchSpace[++index..];
                }

                return count;
            }

            sectionName = default;
            sectionBody = default;
            if (index >= chartText.Length)
                // No more sections present
                return false;

            var search = chartText[index..];

            int nameStartIndex;
            while (true)
            {
                nameStartIndex = search.IndexOf('[');
                if (nameStartIndex < 0)
                {
                    // No more sections present
                    return false;
                }

                int test = nameStartIndex++;
                while (test > 0)
                {
                    --test;
                    if (search[test] > 32 || search[test] == '\n')
                    {
                        break;
                    }
                }

                index += nameStartIndex;

                var curr = search[test];
                search = search[nameStartIndex..];
                if (test == 0 || curr == '\n')
                {
                    break;
                }
            }

            int nameEndIndex = search.IndexOf(']');
            if (nameEndIndex < 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Missing end bracket for section name on line {startLine}!");
            }

            sectionName = search[..nameEndIndex++];
            search = search[nameEndIndex..];
            index += nameEndIndex;

            if (sectionName.IndexOfAny('\r', '\n') >= 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Section name on {startLine} spans across multiple lines!");
            }

            // Find section body
            int sectionStartIndex = search.IndexOf('{');
            if (sectionStartIndex < 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Missing section body for section [{sectionName.ToString()}]! (starting on line {startLine})");
            }
            ++sectionStartIndex;
            search = search[sectionStartIndex..];
            index += sectionStartIndex;

            int sectionEndIndex = 0;
            while (true)
            {
                int sectionEndOffset = search[sectionEndIndex..].IndexOf('}');
                if (sectionEndOffset < 0)
                {
                    int startLine = GetLineCount(chartText, index + sectionEndIndex, nameStartIndex);
                    throw new Exception($"Missing body end bracket for section [{sectionName.ToString()}]! (starting on line {startLine})");
                }

                int test = sectionEndIndex + sectionEndOffset;
                while (test > sectionEndIndex)
                {
                    --test;
                    if (search[test] > 32 || search[test] == '\n')
                    {
                        break;
                    }
                }

                sectionEndIndex += sectionEndOffset;
                if (test == 0 || search[test] == '\n')
                {
                    break;
                }
                ++sectionEndIndex;
            }

            sectionBody = search[..sectionEndIndex].SplitTrimmedAscii('\n');
            index += sectionEndIndex + 1;
            return true;
        }

        private static void SubmitChartData(ref ParseSettings settings, MoonSong song, ReadOnlySpan<char> sectionName,
            AsciiTrimSplitter sectionLines)
        {
            if (sectionName.Equals(ChartIOHelper.SECTION_EVENTS, StringComparison.Ordinal))
            {
                YargLogger.LogTrace("Loading events data");
                SubmitDataGlobals(song, sectionLines);
                return;
            }

            // Determine what difficulty
            foreach (var (diffName, difficulty) in ChartIOHelper.TrackNameToTrackDifficultyLookup)
            {
                if (!sectionName.StartsWith(diffName, StringComparison.Ordinal)) continue;

                foreach (var (instrumentName, instrument) in ChartIOHelper.InstrumentStrToEnumLookup)
                {
                    if (!sectionName.EndsWith(instrumentName, StringComparison.Ordinal)) continue;

                    YargLogger.LogFormatDebug("Loading data for {0} {1}", difficulty, instrument);
                    LoadChart(ref settings, song, sectionLines, instrument, difficulty);
                    break;
                }

                break;
            }
        }

        private static MoonSong SubmitDataSong(AsciiTrimSplitter sectionLines)
        {
            uint resolution = DEFAULT_RESOLUTION;
            foreach (var line in sectionLines)
            {
                var key = line.SplitOnceTrimmed('=', out var value);
                value = value.Trim('"'); // Strip off any quotation marks

                if (key.Equals("Resolution", StringComparison.Ordinal))
                {
                    resolution = (uint)FastInt32Parse(value);
                    break;
                }
            }
            return new MoonSong(resolution);
        }

        private static void SubmitDataSync(MoonSong song, AsciiTrimSplitter sectionLines)
        {
            uint prevTick = 0;

            // This is valid since we are guaranteed to have at least one tempo event at all times
            var tempoTracker = new ChartEventTickTracker<TempoChange>(song.syncTrack.Tempos);
            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty) continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint) FastInt32Parse(tickText);

                    if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    tempoTracker.Update(tick);

                    // Get event type
                    var typeCode = remaining.GetNextWord(out remaining);
                    if (typeCode.Equals("B", StringComparison.Ordinal))
                    {
                        // Get tempo value
                        var tempoText = remaining.GetNextWord(out remaining);
                        uint tempo = (uint) FastInt32Parse(tempoText);

                        song.Add(new TempoChange(tempo / 1000f, song.TickToTime(tick, tempoTracker.Current!), tick));
                    }
                    else if (typeCode.Equals("TS", StringComparison.Ordinal))
                    {
                        // Get numerator
                        var numeratorText = remaining.GetNextWord(out remaining);
                        uint numerator = (uint) FastInt32Parse(numeratorText);

                        // Get denominator
                        var denominatorText = remaining.GetNextWord(out remaining);
                        uint denominator = denominatorText.IsEmpty ? 2 : (uint) FastInt32Parse(denominatorText);
                        song.Add(new TimeSignatureChange(numerator, (uint) Math.Pow(2, denominator),
                            song.TickToTime(tick, tempoTracker.Current!), tick));
                    }
                    else if (typeCode.Equals("A", StringComparison.Ordinal))
                    {
                        // Ignored for now, we don't need anchors
                    }
                    else
                    {
                        YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCode.ToString());
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }
        }

        private static void SubmitDataGlobals(MoonSong song, AsciiTrimSplitter sectionLines)
        {
            uint prevTick = 0;
            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty) continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint) FastInt32Parse(tickText);

                    if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    // Get event type
                    var typeCodeText = remaining.GetNextWord(out remaining);
                    if (typeCodeText[0] == 'E')
                    {
                        // Get event text
                        var eventText = TextEvents.NormalizeTextEvent(remaining.TrimOnce('"').Trim());

                        // Check for section events
                        if (TextEvents.TryParseSectionEvent(eventText, out var sectionName))
                        {
                            song.sections.Add(new MoonText(sectionName.ToString(), tick));
                        }
                        else
                        {
                            song.events.Add(new MoonText(eventText.ToString(), tick));
                        }
                    }
                    else
                    {
                        YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCodeText[0]);
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }
        }

        #region Utility

        #endregion

        private static void LoadChart(ref ParseSettings settings, MoonSong song, AsciiTrimSplitter sectionLines,
            MoonSong.MoonInstrument instrument, MoonSong.Difficulty difficulty)
        {
            var chart = song.GetChart(instrument, difficulty);
            var gameMode = chart.gameMode;

            var flags = new List<NoteFlag>();
            var postNotesAddedProcessList = GetInitialPostProcessList(gameMode);

            var processParams = new NoteProcessParams()
            {
                chart = chart,
                settings = settings,
                postNotesAddedProcessList = postNotesAddedProcessList
            };

            chart.notes.Capacity = 5000;

            var noteProcessDict = GetNoteProcessDict(gameMode);
            var specialPhraseProcessDict = GetSpecialPhraseProcessDict(gameMode);

            try
            {
                uint prevTick = 0;
                // Load notes, collect flags
                foreach (var line in sectionLines)
                {
                    try
                    {
                        // Split on the equals sign
                        var tickText = line.SplitOnceTrimmed('=', out var remaining);

                        // Get tick
                        uint tick = (uint) FastInt32Parse(tickText);

                        if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                        prevTick = tick;

                        // Get event type
                        char typeCode = remaining.GetNextWord(out remaining)[0];
                        switch (
                            typeCode) // Note this will need to be changed if keys are ever greater than 1 character long
                        {
                            case 'N':
                            {
                                // Get note data
                                var noteTypeText = remaining.GetNextWord(out remaining);
                                int noteType = FastInt32Parse(noteTypeText);

                                var noteLengthText = remaining.GetNextWord(out remaining);
                                uint noteLength = (uint) FastInt32Parse(noteLengthText);

                                // Process the note
                                if (noteProcessDict.TryGetValue(noteType, out var processFn))
                                {
                                    var noteEvent = new NoteEvent()
                                    {
                                        tick = tick,
                                        noteNumber = noteType,
                                        length = noteLength
                                    };
                                    processParams.noteEvent = noteEvent;
                                    processFn(ref processParams);
                                }

                                break;
                            }

                            case 'S':
                            {
                                // Get phrase data
                                var phraseTypeText = remaining.GetNextWord(out remaining);
                                int phraseType = FastInt32Parse(phraseTypeText);

                                var phraseLengthText = remaining.GetNextWord(out remaining);
                                uint phraseLength = (uint) FastInt32Parse(phraseLengthText);

                                if (specialPhraseProcessDict.TryGetValue(phraseType, out var processFn))
                                {
                                    var noteEvent = new NoteEvent()
                                    {
                                        tick = tick,
                                        noteNumber = phraseType,
                                        length = phraseLength
                                    };
                                    processParams.noteEvent = noteEvent;
                                    processFn(ref processParams);
                                }

                                break;
                            }
                            case 'E':
                            {
                                var eventText = TextEvents.NormalizeTextEvent(remaining.TrimOnce('"'));
                                chart.events.Add(new MoonText(eventText.ToString(), tick));
                                break;
                            }

                            default:
                                YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCode);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                    }
                }

                foreach (var fn in postNotesAddedProcessList)
                {
                    fn(ref processParams);
                }

                chart.notes.TrimExcess();
                settings = processParams.settings;
            }
            catch (Exception e)
            {
                // Bad load, most likely a parsing error
                YargLogger.LogException(e, $"Error parsing .chart section for {difficulty} {instrument}!");
                chart.Clear();
            }
        }

        private static void ProcessNoteOnEventAsNote(ref NoteProcessParams noteProcessParams, int ingameFret,
            MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;
            if (sus < noteProcessParams.settings.SustainCutoffThreshold) sus = 0;

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            MoonObjectHelper.PushNote(newMoonNote, chart.notes);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(ref NoteProcessParams noteProcessParams,
            MoonPhrase.Type type)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;

            var newPhrase = new MoonPhrase(tick, sus, type);
            chart.specialPhrases.Add(newPhrase);
        }

        private static void ProcessNoteOnEventAsChordFlag(ref NoteProcessParams noteProcessParams,
            NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((ref NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsChordFlagPostDelay(ref processParams, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsChordFlagPostDelay(ref NoteProcessParams noteProcessParams,
            NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            MoonObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
            if (length > 0)
            {
                GroupAddFlags(chart.notes, flagData, index, length);
            }
        }

        private static void ProcessNoteOnEventAsNoteFlagToggle(ref NoteProcessParams noteProcessParams, int rawNote,
            NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((ref NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsNoteFlagTogglePostDelay(ref processParams, rawNote, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsNoteFlagTogglePostDelay(ref NoteProcessParams noteProcessParams,
            int rawNote, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            MoonObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
            if (length > 0)
            {
                for (int i = index; i < index + length; ++i)
                {
                    var note = chart.notes[i];
                    if (note.rawNote == rawNote)
                    {
                        TryAddNoteFlags(note, flagData);
                    }
                }
            }
        }

        private static void GroupAddFlags(IList<MoonNote> notes, NoteFlagPriority flagData, int index, int length)
        {
            for (int i = index; i < index + length; ++i)
            {
                TryAddNoteFlags(notes[i], flagData);
            }
        }

        private static void TryAddNoteFlags(MoonNote note, NoteFlagPriority flagData)
        {
            if (!flagData.TryApplyToNote(note))
            {
                YargLogger.LogFormatDebug("Could not apply flag {0} to a note. It was blocked by existing flag {1}.",
                    flagData.flagToAdd, flagData.blockingFlag);
            }
        }
    }
}