// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using NAudio.Midi;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song.IO
{
    public static class MidReader
    {
        const int SOLO_END_CORRECTION_OFFSET = -1;

        public enum CallbackState
        {
            None,
            WaitingForExternalInformation,
        }

        static readonly IReadOnlyDictionary<string, Song.Instrument> c_trackNameToInstrumentMap = new Dictionary<string, Song.Instrument>()
        {
            { MidIOHelper.GUITAR_TRACK,        Song.Instrument.Guitar },
            { MidIOHelper.GH1_GUITAR_TRACK,    Song.Instrument.Guitar },
            { MidIOHelper.GUITAR_COOP_TRACK,   Song.Instrument.GuitarCoop },
            { MidIOHelper.BASS_TRACK,          Song.Instrument.Bass },
            { MidIOHelper.RHYTHM_TRACK,        Song.Instrument.Rhythm },
            { MidIOHelper.KEYS_TRACK,          Song.Instrument.Keys },
            { MidIOHelper.DRUMS_TRACK,         Song.Instrument.Drums },
            { MidIOHelper.DRUMS_REAL_TRACK,    Song.Instrument.Drums },
            { MidIOHelper.GHL_GUITAR_TRACK,    Song.Instrument.GHLiveGuitar },
            { MidIOHelper.GHL_BASS_TRACK,      Song.Instrument.GHLiveBass },
            { MidIOHelper.GHL_RHYTHM_TRACK,    Song.Instrument.GHLiveRhythm },
            { MidIOHelper.GHL_GUITAR_COOP_TRACK, Song.Instrument.GHLiveCoop },
        };

        static readonly IReadOnlyDictionary<string, bool> c_trackExcludesMap = new Dictionary<string, bool>()
        {
            { MidIOHelper.BEAT_TRACK,       true },
            { MidIOHelper.VENUE_TRACK,      true },
        };

        static readonly List<Song.Instrument> c_legacyStarPowerFixupWhitelist = new List<Song.Instrument>()
        {
            Song.Instrument.Guitar,
            Song.Instrument.GuitarCoop,
            Song.Instrument.Bass,
            Song.Instrument.Rhythm,
        };

        static readonly IReadOnlyDictionary<Song.AudioInstrument, string[]> c_audioStreamLocationOverrideDict = new Dictionary<Song.AudioInstrument, string[]>()
        {
            // String list is ordered in priority. If it finds a file names with the first string it'll skip over the rest.
            // Otherwise just does a ToString on the AudioInstrument enum
            { Song.AudioInstrument.Drum, new string[] { "drums", "drums_1" } },
        };

        struct EventProcessParams
        {
            public Song song;
            public Song.Instrument instrument;
            public Chart currentUnrecognisedChart;
            public MidiEvent midiEvent;
            public IReadOnlyDictionary<int, EventProcessFn> noteProcessMap;
            public IReadOnlyDictionary<string, ProcessModificationProcessFn> textProcessMap;
            public IReadOnlyDictionary<byte, EventProcessFn> sysexProcessMap;
            public List<EventProcessFn> delayedProcessesList;
        }

        // Delegate for functions that parse something into the chart
        delegate void EventProcessFn(in EventProcessParams eventProcessParams);
        // Delegate for functions that modify how the chart should be parsed
        delegate void ProcessModificationProcessFn(ref EventProcessParams eventProcessParams);

        // These dictionaries map the NoteNumber of each midi note event to a specific function of how to process them
        static readonly IReadOnlyDictionary<int, EventProcessFn> GuitarMidiNoteNumberToProcessFnMap = BuildGuitarMidiNoteNumberToProcessFnDict();
        static readonly IReadOnlyDictionary<int, EventProcessFn> GuitarMidiNoteNumberToProcessFnMap_EnhancedOpens = BuildGuitarMidiNoteNumberToProcessFnDict(enhancedOpens: true);
        static readonly IReadOnlyDictionary<int, EventProcessFn> GhlGuitarMidiNoteNumberToProcessFnMap = BuildGhlGuitarMidiNoteNumberToProcessFnDict();
        static readonly IReadOnlyDictionary<int, EventProcessFn> DrumsMidiNoteNumberToProcessFnMap = BuildDrumsMidiNoteNumberToProcessFnDict();
        static readonly IReadOnlyDictionary<int, EventProcessFn> DrumsMidiNoteNumberToProcessFnMap_Velocity = BuildDrumsMidiNoteNumberToProcessFnDict(enableVelocity: true);

        // These dictionaries map the text of a MIDI text event to a specific function that processes them
        static readonly IReadOnlyDictionary<string, ProcessModificationProcessFn> GuitarTextEventToProcessFnMap = new Dictionary<string, ProcessModificationProcessFn>()
        {
            { MidIOHelper.ENHANCED_OPENS_TEXT, SwitchToGuitarEnhancedOpensProcessMap },
            { MidIOHelper.ENHANCED_OPENS_TEXT_BRACKET, SwitchToGuitarEnhancedOpensProcessMap }
        };

        static readonly IReadOnlyDictionary<string, ProcessModificationProcessFn> GhlGuitarTextEventToProcessFnMap = new Dictionary<string, ProcessModificationProcessFn>()
        {
        };

        static readonly IReadOnlyDictionary<string, ProcessModificationProcessFn> DrumsTextEventToProcessFnMap = new Dictionary<string, ProcessModificationProcessFn>()
        {
            { MidIOHelper.CHART_DYNAMICS_TEXT, SwitchToDrumsVelocityProcessMap },
            { MidIOHelper.CHART_DYNAMICS_TEXT_BRACKET, SwitchToDrumsVelocityProcessMap },
        };

        static readonly Dictionary<byte, EventProcessFn> GuitarSysExEventToProcessFnMap = new Dictionary<byte, EventProcessFn>()
        {
            { MidIOHelper.SYSEX_CODE_GUITAR_OPEN, ProcessSysExEventPairAsOpenNoteModifier },
            { MidIOHelper.SYSEX_CODE_GUITAR_TAP, (in EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsForcedType(eventProcessParams, Note.NoteType.Tap);
            }},
        };

        static readonly Dictionary<byte, EventProcessFn> GhlGuitarSysExEventToProcessFnMap = new Dictionary<byte, EventProcessFn>()
        {
            { MidIOHelper.SYSEX_CODE_GUITAR_OPEN, ProcessSysExEventPairAsOpenNoteModifier },
            { MidIOHelper.SYSEX_CODE_GUITAR_TAP, (in EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsForcedType(eventProcessParams, Note.NoteType.Tap);
            }},
        };

        static readonly Dictionary<byte, EventProcessFn> DrumsSysExEventToProcessFnMap = new Dictionary<byte, EventProcessFn>()
        {
        };

        // For handling things that require user intervention
        delegate void MessageProcessFn(MessageProcessParams processParams);
        struct MessageProcessParams
        {
            public string title;
            public string message;
            public Song currentSong;
            public Song.Instrument instrument;
            public int trackNumber;
            public MessageProcessFn processFn;
            public bool executeInEditor;
        }
        public static Song ReadMidi(string path, ref CallbackState callBackState)
        {
            // Initialize new song
            Song song = new Song();
            string directory = Path.GetDirectoryName(path);

            // Make message list
            var messageList = new List<MessageProcessParams>();

            foreach (Song.AudioInstrument audio in EnumX<Song.AudioInstrument>.Values)
            {
                // First try any specific filenames for the instrument, then try the instrument name
                List<string> filenamesToTry = new List<string>();

                if (c_audioStreamLocationOverrideDict.ContainsKey(audio)) {
                    filenamesToTry.AddRange(c_audioStreamLocationOverrideDict[audio]);
                }

                filenamesToTry.Add(audio.ToString());

                // Search for each combination of filenamesToTry + audio extension until we find a file
                string audioFilepath = null;

                foreach (string testFilename in filenamesToTry)
                {
                    foreach (string extension in Globals.validAudioExtensions) {
                        string testFilepath = Path.Combine(directory, testFilename.ToLower() + extension);

                        if (File.Exists(testFilepath))
                        {
                            audioFilepath = testFilepath;
                            break;
                        }
                    }
                }

                // If we didn't find a file, assign a default value to the audio path
                if (audioFilepath == null) {
                    audioFilepath = Path.Combine(directory, audio.ToString().ToLower() + ".ogg");
                }

                Debug.Log(audioFilepath);
                song.SetAudioLocation(audio, audioFilepath);
            }

            MidiFile midi;

            try
            {
                midi = new MidiFile(path);
            }
            catch (SystemException e)
            {
                throw new SystemException("Bad or corrupted midi file- " + e.Message);
            }

            if (midi.Events == null || midi.Tracks < 1)
            {
                throw new InvalidOperationException("MIDI file has no tracks, unable to parse.");
            }

            song.resolution = (short)midi.DeltaTicksPerQuarterNote;

            // Read all bpm data in first. This will also allow song.TimeToTick to function properly.
            ReadSync(midi.Events[0], song);

            for (int i = 1; i < midi.Tracks; ++i)
            {
                var track = midi.Events[i];
                if (track == null || track.Count < 1)
                {
                    Debug.LogWarning($"Track {i} is null or empty.");
                    continue;
                }

                var trackName = track[0] as TextEvent;
                if (trackName == null)
                    continue;
                Debug.Log("Found midi track " + trackName.Text);

                string trackNameKey = trackName.Text.ToUpper();
                if (c_trackExcludesMap.ContainsKey(trackNameKey))
                {
                    continue;
                }

                switch (trackNameKey)
                {
                    case MidIOHelper.EVENTS_TRACK:
                        ReadSongGlobalEvents(track, song);
                        break;

                    case MidIOHelper.VOCALS_TRACK:
                        messageList.Add(new MessageProcessParams()
                        {
                            message = "A vocals track was found in the file. Would you like to import the text events as global lyrics and phrase events?",
                            title = "Vocals Track Found",
                            executeInEditor = true,
                            currentSong = song,
                            trackNumber = i,
                            processFn = (MessageProcessParams processParams) => {
                                Debug.Log("Loading lyrics from Vocals track");
                                ReadTextEventsIntoGlobalEventsAsLyrics(midi.Events[processParams.trackNumber], processParams.currentSong);
                            }
                        });
                        break;

                    default:
                        Song.Instrument instrument;
                        if (!c_trackNameToInstrumentMap.TryGetValue(trackNameKey, out instrument))
                        {
                            instrument = Song.Instrument.Unrecognised;
                        }

                        if ((instrument != Song.Instrument.Unrecognised) && song.ChartExistsForInstrument(instrument))
                        {
                            messageList.Add(new MessageProcessParams()
                            {
                                message = $"A track was already loaded for instrument {instrument}, but another track was found for this instrument: {trackNameKey}\nWould you like to overwrite the currently loaded track?",
                                title = "Duplicate Instrument Track Found",
                                executeInEditor = false,
                                currentSong = song,
                                trackNumber = i,
                                processFn = (MessageProcessParams processParams) => {
                                    Debug.Log($"Overwriting already-loaded part {processParams.instrument}");
                                    foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
                                    {
                                        var chart = processParams.currentSong.GetChart(processParams.instrument, difficulty);
                                        chart.Clear();
                                        chart.UpdateCache();
                                    }

                                    ReadNotes(midi.Events[processParams.trackNumber], messageList, processParams.currentSong, processParams.instrument);
                                }
                            });
                        }
                        else
                        {
                            Debug.LogFormat("Loading midi track {0}", instrument);
                            ReadNotes(track, messageList, song, instrument);
                        }
                        break;
                }
            }

            // Display messages to user
            ProcessPendingUserMessages(messageList, ref callBackState);

            return song;
        }

        static void ProcessPendingUserMessages(IList<MessageProcessParams> messageList, ref CallbackState callBackState)
        {
            if (messageList == null)
            {
                Debug.Assert(false, $"No message list provided to {nameof(ProcessPendingUserMessages)}!");
                return;
            }

            foreach (var processParams in messageList)
            {
#if UNITY_EDITOR
                // The editor freezes when its message box API is used during parsing,
                // we use the params to determine whether or not to execute actions instead
                if (!processParams.executeInEditor)
                {
                    Debug.Log("Auto-skipping action for message: " + processParams.message);
                    continue;
                }
                else
                {
                    Debug.Log("Auto-executing action for message: " + processParams.message);
                    processParams.processFn(processParams);
                }
#else
                callBackState = CallbackState.WaitingForExternalInformation;
                NativeMessageBox.Result result = NativeMessageBox.Show(processParams.message, processParams.title, NativeMessageBox.Type.YesNo, null);
                callBackState = CallbackState.None;
                if (result == NativeMessageBox.Result.Yes)
                {
                    processParams.processFn(processParams);
                }
#endif
            }
        }

        static void ReadTrack(IList<MidiEvent> track)
        {
            foreach (var me in track)
            {
                var note = me as NoteOnEvent;
                if (note != null)
                {
                    Debug.Log("Note: " + note.NoteNumber + ", Pos: " + note.AbsoluteTime + ", Vel: " + note.Velocity + ", Channel: " + note.Channel + ", Off pos: " + note.OffEvent.AbsoluteTime);
                }

                var text = me as TextEvent;
                if (text != null)
                {
                    Debug.Log(text.Text + " " + text.AbsoluteTime);
                }
            }
        }

        private static void ReadSync(IList<MidiEvent> track, Song song)
        {
            foreach (var me in track)
            {
                var ts = me as TimeSignatureEvent;
                if (ts != null)
                {
                    var tick = me.AbsoluteTime;

                    song.Add(new TimeSignature((uint)tick, (uint)ts.Numerator, (uint)(Mathf.Pow(2, ts.Denominator))), false);
                    continue;
                }
                var tempo = me as TempoEvent;
                if (tempo != null)
                {
                    var tick = me.AbsoluteTime;
                    song.Add(new BPM((uint)tick, (uint)(tempo.Tempo * 1000)), false);
                    continue;
                }

                // Read the song name
                var text = me as TextEvent;
                if (text != null)
                {
                    song.name = text.Text;
                }
            }

            song.UpdateCache();
        }

        private static void ReadSongGlobalEvents(IList<MidiEvent> track, Song song)
        {
            const string rb2SectionPrefix = "[" + MidIOHelper.SECTION_PREFIX_RB2;
            const string rb3SectionPrefix = "[" + MidIOHelper.SECTION_PREFIX_RB3;

            for (int i = 1; i < track.Count; ++i)
            {
                var text = track[i] as TextEvent;

                if (text != null)
                {
                    if (text.Text.Contains(rb2SectionPrefix))
                    {
                        song.Add(new Section(text.Text.Substring(9, text.Text.Length - 10), (uint)text.AbsoluteTime), false);
                    }
                    else if (text.Text.Contains(rb3SectionPrefix) && text.Text.Length > 1)
                    {
                        string sectionText = string.Empty;
                        char lastChar = text.Text[text.Text.Length - 1];
                        if (lastChar == ']')
                        {
                            sectionText = text.Text.Substring(5, text.Text.Length - 6);
                        }
                        else if (lastChar == '"')
                        {
                            // Is in the format [prc_intro] "Intro". Strip for just the quoted section
                            int startIndex = text.Text.IndexOf('"') + 1;
                            sectionText = text.Text.Substring(startIndex, text.Text.Length - (startIndex + 1));
                        }
                        else
                        {
                            Debug.LogError("Found section name in an unknown format: " + text.Text);
                        }

                        song.Add(new Section(sectionText, (uint)text.AbsoluteTime), false);
                    }
                    else
                    {
                        song.Add(new Event(text.Text.Trim(new char[] { '[', ']' }), (uint)text.AbsoluteTime), false);
                    }
                }
            }

            song.UpdateCache();
        }

        private static void ReadTextEventsIntoGlobalEventsAsLyrics(IList<MidiEvent> track, Song song)
        {
            for (int i = 1; i < track.Count; ++i)
            {
                var text = track[i] as TextEvent;
                if (text != null && text.Text.Length > 0 && text.MetaEventType == MetaEventType.Lyric)
                {
                    string lyricEvent = MidIOHelper.LYRIC_EVENT_PREFIX + text.Text;
                    song.Add(new Event(lyricEvent, (uint)text.AbsoluteTime), false);
                }

                var phrase = track[i] as NoteOnEvent;
                if (phrase != null && phrase.OffEvent != null &&
                    (phrase.NoteNumber == MidIOHelper.LYRICS_PHRASE_1 || phrase.NoteNumber == MidIOHelper.LYRICS_PHRASE_2))
                {
                    string phraseStartEvent = MidIOHelper.LYRICS_PHRASE_START_TEXT;
                    song.Add(new Event(phraseStartEvent, (uint)phrase.AbsoluteTime), false);

                    string phraseEndEvent = MidIOHelper.LYRICS_PHRASE_END_TEXT;
                    song.Add(new Event(phraseEndEvent, (uint)phrase.OffEvent.AbsoluteTime), false);
                }
            }

            song.UpdateCache();
        }

        private static void ReadNotes(IList<MidiEvent> track, IList<MessageProcessParams> messageList, Song song, Song.Instrument instrument)
        {
            if (track == null || track.Count < 1)
            {
                Debug.LogError($"Attempted to load null or empty track.");
                return;
            }

            Debug.Assert(messageList != null, $"No message list provided to {nameof(ReadNotes)}!");

            var sysexEventQueue = new List<PhaseShiftSysExStart>();

            Chart unrecognised = new Chart(song, Song.Instrument.Unrecognised);
            Chart.GameMode gameMode = Song.InstumentToChartGameMode(instrument);

            EventProcessParams processParams = new EventProcessParams()
            {
                song = song,
                currentUnrecognisedChart = unrecognised,
                instrument = instrument,
                noteProcessMap = GetNoteProcessDict(gameMode),
                textProcessMap = GetTextEventProcessDict(gameMode),
                sysexProcessMap = GetSysExEventProcessDict(gameMode),
                delayedProcessesList = new List<EventProcessFn>(),
            };

            if (instrument == Song.Instrument.Unrecognised)
            {
                var text = track[0] as TextEvent;
                if (text != null)
                    unrecognised.name = text.Text;
                song.unrecognisedCharts.Add(unrecognised);
            }

            // Load all the notes
            for (int i = 1; i < track.Count; i++)
            {
                var text = track[i] as TextEvent;
                if (text != null)
                {
                    var tick = (uint)text.AbsoluteTime;
                    var eventName = text.Text;

                    ChartEvent chartEvent = new ChartEvent(tick, eventName);

                    if (instrument == Song.Instrument.Unrecognised)
                    {
                        unrecognised.Add(chartEvent);
                    }
                    else
                    {
                        ProcessModificationProcessFn processFn;
                        if (processParams.textProcessMap.TryGetValue(eventName, out processFn))
                        {
                            // This text event affects parsing of the .mid file, run its function and don't parse it into the chart
                            processParams.midiEvent = text;
                            processFn(ref processParams);
                        }
                        else
                        {
                            // Copy text event to all difficulties so that .chart format can store these properly. Midi writer will strip duplicate events just fine anyway.
                            foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
                            {
                                song.GetChart(instrument, difficulty).Add(chartEvent);
                            }
                        }
                    }
                }

                var note = track[i] as NoteOnEvent;
                if (note != null && note.OffEvent != null)
                {
                    if (instrument == Song.Instrument.Unrecognised)
                    {
                        var tick = (uint)note.AbsoluteTime;
                        var sus = CalculateSustainLength(song, note);

                        int rawNote = note.NoteNumber;
                        Note newNote = new Note(tick, rawNote, sus);
                        unrecognised.Add(newNote);
                        continue;
                    }

                    processParams.midiEvent = note;

                    EventProcessFn processFn;
                    if (processParams.noteProcessMap.TryGetValue(note.NoteNumber, out processFn))
                    {
                        processFn(processParams);
                    }
                }

                var sysexEvent = track[i] as SysexEvent;
                if (sysexEvent != null)
                {
                    PhaseShiftSysEx psEvent;
                    if (!PhaseShiftSysEx.TryParse(sysexEvent, out psEvent))
                    {
                        // SysEx event is not a Phase Shift SysEx event
                        Debug.LogWarning($"Encountered unknown SysEx event: {BitConverter.ToString(sysexEvent.GetData())}");
                        continue;
                    }

                    if (psEvent.type != MidIOHelper.SYSEX_TYPE_PHRASE)
                    {
                        Debug.LogWarning($"Encountered unknown Phase Shift SysEx event type {psEvent.type}");
                        continue;
                    }

                    if (psEvent.value == MidIOHelper.SYSEX_VALUE_PHRASE_START)
                    {
                        sysexEventQueue.Add(new PhaseShiftSysExStart(psEvent));
                    }
                    else if (psEvent.value == MidIOHelper.SYSEX_VALUE_PHRASE_END && TryPairSysExEvents(sysexEventQueue, psEvent, out var eventPair, out int index))
                    {
                        sysexEventQueue.RemoveAt(index);
                        processParams.midiEvent = eventPair;
                        EventProcessFn processFn;
                        if (processParams.sysexProcessMap.TryGetValue(psEvent.code, out processFn))
                        {
                            processFn(processParams);
                        }
                    }
                }
            }

            Debug.Assert(sysexEventQueue.Count == 0, $"SysEx event queue was not fully processed! Remaining event count: {sysexEventQueue.Count}");

            // Update all chart arrays
            if (instrument != Song.Instrument.Unrecognised)
            {
                foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
                    song.GetChart(instrument, diff).UpdateCache();
            }
            else
                unrecognised.UpdateCache();

            // Apply forcing events
            foreach (var process in processParams.delayedProcessesList)
            {
                process(processParams);
            }

            // Legacy star power fixup
            if (c_legacyStarPowerFixupWhitelist.Contains(instrument))
            {
                // Only need to check one difficulty since Star Power gets copied to all difficulties
                var chart = song.GetChart(instrument, Song.Difficulty.Expert);
                if (chart.starPower.Count <= 0 && (ContainsTextEvent(chart.events, MidIOHelper.SOLO_EVENT_TEXT) || ContainsTextEvent(chart.events, MidIOHelper.SOLO_END_EVENT_TEXT)))
                {
                    TextEvent text = track[0] as TextEvent;
                    Debug.Assert(text != null, "Track name not found when processing legacy starpower fixups");
                    messageList?.Add(new MessageProcessParams()
                    {
                        message = $"No Star Power phrases were found on track {text.Text}. However, solo phrases were found. These may be legacy star power phrases.\nImport these solo phrases as Star Power?",
                        title = "Legacy Star Power Detected",
                        executeInEditor = true,
                        currentSong = processParams.song,
                        instrument = processParams.instrument,
                        processFn = (messageParams) => {
                            Debug.Log("Loading solo events as Star Power");
                            ProcessTextEventPairAsStarpower(
                                new EventProcessParams()
                                {
                                    song = messageParams.currentSong,
                                    instrument = messageParams.instrument
                                },
                                MidIOHelper.SOLO_EVENT_TEXT,
                                MidIOHelper.SOLO_END_EVENT_TEXT,
                                Starpower.Flags.None
                            );
                            // Update instrument's caches
                            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
                                messageParams.currentSong.GetChart(messageParams.instrument, diff).UpdateCache();
                        }
                    });
                }
            }
        }

        static bool ContainsTextEvent(IList<ChartEvent> events, string text)
        {
            foreach (var textEvent in events)
            {
                if (textEvent.eventName == text)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryPairSysExEvents(IList<PhaseShiftSysExStart> startEvents, PhaseShiftSysEx endEvent, out PhaseShiftSysExStart eventPair, out int index)
        {
            if (startEvents == null)
                throw new ArgumentNullException(nameof(startEvents));

            if (endEvent == null)
                throw new ArgumentNullException(nameof(endEvent));

            // Iterate through event queue in reverse to find the corresponding start event
            for (int startIndex = startEvents.Count - 1; startIndex >= 0; startIndex--)
            {
                var startEvent = startEvents[startIndex];
                if (startEvent.AbsoluteTime <= endEvent.AbsoluteTime && startEvent.MatchesWith(endEvent))
                {
                    startEvent.endEvent = endEvent;
                    eventPair = startEvent;
                    index = startIndex;
                    return true;
                }
            }

            Debug.Assert(false, $"SysEx end event without a matching start event!\n{endEvent}");
            eventPair = null;
            index = -1;
            return false;
        }

        static IReadOnlyDictionary<int, EventProcessFn> GetNoteProcessDict(Chart.GameMode gameMode)
        {
            switch (gameMode)
            {
                case Chart.GameMode.GHLGuitar:
                    {
                        return GhlGuitarMidiNoteNumberToProcessFnMap;
                    }
                case Chart.GameMode.Drums:
                    {
                        return DrumsMidiNoteNumberToProcessFnMap;
                    }

                default: break;
            }

            return GuitarMidiNoteNumberToProcessFnMap;
        }

        static IReadOnlyDictionary<string, ProcessModificationProcessFn> GetTextEventProcessDict(Chart.GameMode gameMode)
        {
            switch (gameMode)
            {
                case Chart.GameMode.GHLGuitar:
                    return GhlGuitarTextEventToProcessFnMap;
                case Chart.GameMode.Drums:
                    return DrumsTextEventToProcessFnMap;

                default:
                    return GuitarTextEventToProcessFnMap;
            }
        }

        static IReadOnlyDictionary<byte, EventProcessFn> GetSysExEventProcessDict(Chart.GameMode gameMode)
        {
            switch (gameMode)
            {
                case Chart.GameMode.Guitar:
                    return GuitarSysExEventToProcessFnMap;
                case Chart.GameMode.GHLGuitar:
                    return GhlGuitarSysExEventToProcessFnMap;
                case Chart.GameMode.Drums:
                    return DrumsSysExEventToProcessFnMap;

                // Don't process any SysEx events on unrecognized tracks
                default:
                    return new Dictionary<byte, EventProcessFn>();
            }
        }

        static void SwitchToGuitarEnhancedOpensProcessMap(ref EventProcessParams processParams)
        {
            var gameMode = Song.InstumentToChartGameMode(processParams.instrument);
            if (gameMode != Chart.GameMode.Guitar)
            {
                Debug.LogWarning($"Attempted to apply guitar enhanced opens process map to non-guitar instrument: {processParams.instrument}");
                return;
            }

            // Switch process map to guitar enhanced opens process map
            processParams.noteProcessMap = GuitarMidiNoteNumberToProcessFnMap_EnhancedOpens;
        }

        static void SwitchToDrumsVelocityProcessMap(ref EventProcessParams processParams)
        {
            if (processParams.instrument != Song.Instrument.Drums)
            {
                Debug.LogWarning($"Attempted to apply drums velocity process map to non-drums instrument: {processParams.instrument}");
                return;
            }

            // Switch process map to drums velocity process map
            processParams.noteProcessMap = DrumsMidiNoteNumberToProcessFnMap_Velocity;
        }

        static IReadOnlyDictionary<int, EventProcessFn> BuildGuitarMidiNoteNumberToProcessFnDict(bool enhancedOpens = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, Note.NoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, 0, MidIOHelper.SOLO_END_EVENT_TEXT, SOLO_END_CORRECTION_OFFSET);
                }},
            };

            var FretToMidiKey = new Dictionary<Note.GuitarFret, int>()
            {
                { Note.GuitarFret.Green, 0 },
                { Note.GuitarFret.Red, 1 },
                { Note.GuitarFret.Yellow, 2 },
                { Note.GuitarFret.Blue, 3 },
                { Note.GuitarFret.Orange, 4 },
            };

            if (enhancedOpens)
                FretToMidiKey.Add(Note.GuitarFret.Open, -1);

            foreach (var difficulty in EnumX<Song.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumX<Note.GuitarFret>.Values)
                {
                    int fretOffset;
                    if (FretToMidiKey.TryGetValue(guitarFret, out fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 5;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, Note.NoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 6;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, Note.NoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        static IReadOnlyDictionary<int, EventProcessFn> BuildGhlGuitarMidiNoteNumberToProcessFnDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, Note.NoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, 0, MidIOHelper.SOLO_END_EVENT_TEXT, SOLO_END_CORRECTION_OFFSET);
                }},
            };

            IReadOnlyDictionary<Note.GHLiveGuitarFret, int> FretToMidiKey = new Dictionary<Note.GHLiveGuitarFret, int>()
            {
                { Note.GHLiveGuitarFret.Open, 0 },
                { Note.GHLiveGuitarFret.White1, 1 },
                { Note.GHLiveGuitarFret.White2, 2 },
                { Note.GHLiveGuitarFret.White3, 3 },
                { Note.GHLiveGuitarFret.Black1, 4 },
                { Note.GHLiveGuitarFret.Black2, 5 },
                { Note.GHLiveGuitarFret.Black3, 6 },
            };

            foreach (var difficulty in EnumX<Song.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GHL_GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumX<Note.GHLiveGuitarFret>.Values)
                {
                    int fretOffset;
                    if (FretToMidiKey.TryGetValue(guitarFret, out fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 7;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, Note.NoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 8;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, Note.NoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        static IReadOnlyDictionary<int, EventProcessFn> BuildDrumsMidiNoteNumberToProcessFnDict(bool enableVelocity = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, 0, MidIOHelper.SOLO_END_EVENT_TEXT, SOLO_END_CORRECTION_OFFSET);
                }},
                { MidIOHelper.DOUBLE_KICK_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsNote(eventProcessParams, Song.Difficulty.Expert, (int)Note.DrumPad.Kick, Note.Flags.InstrumentPlus);
                }},

                { MidIOHelper.STARPOWER_DRUM_FILL_0, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.STARPOWER_DRUM_FILL_1, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.STARPOWER_DRUM_FILL_2, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.STARPOWER_DRUM_FILL_3, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.STARPOWER_DRUM_FILL_4, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.DRUM_ROLL_STANDARD, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsDrumRoll(eventProcessParams, DrumRoll.Type.Standard);
                }},
                { MidIOHelper.DRUM_ROLL_SPECIAL, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsDrumRoll(eventProcessParams, DrumRoll.Type.Special);
                }},
            };

            IReadOnlyDictionary<Note.DrumPad, int> DrumPadToMidiKey = new Dictionary<Note.DrumPad, int>()
            {
                { Note.DrumPad.Kick, 0 },
                { Note.DrumPad.Red, 1 },
                { Note.DrumPad.Yellow, 2 },
                { Note.DrumPad.Blue, 3 },
                { Note.DrumPad.Orange, 4 },
                { Note.DrumPad.Green, 5 },
            };

            IReadOnlyDictionary<Note.DrumPad, Note.Flags> DrumPadDefaultFlags = new Dictionary<Note.DrumPad, Note.Flags>()
            {
                { Note.DrumPad.Yellow, Note.Flags.ProDrums_Cymbal },
                { Note.DrumPad.Blue, Note.Flags.ProDrums_Cymbal },
                { Note.DrumPad.Orange, Note.Flags.ProDrums_Cymbal },
            };

            foreach (var difficulty in EnumX<Song.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.DRUMS_DIFF_START_LOOKUP[difficulty];
                foreach (var pad in EnumX<Note.DrumPad>.Values)
                {
                    int padOffset;
                    if (DrumPadToMidiKey.TryGetValue(pad, out padOffset))
                    {
                        int key = padOffset + difficultyStartRange;
                        int fret = (int)pad;
                        Note.Flags defaultFlags = Note.Flags.None;
                        DrumPadDefaultFlags.TryGetValue(pad, out defaultFlags);

                        if (enableVelocity && pad != Note.DrumPad.Kick)
                        {
                            processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                            {
                                var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
                                Debug.Assert(noteEvent != null, $"Wrong note event type passed to drums note process. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");

                                var flags = defaultFlags;
                                switch (noteEvent.Velocity)
                                {
                                    case MidIOHelper.VELOCITY_ACCENT:
                                        {
                                            flags |= Note.Flags.ProDrums_Accent;
                                            break;
                                        }
                                    case MidIOHelper.VELOCITY_GHOST:
                                        {
                                            flags |= Note.Flags.ProDrums_Ghost;
                                            break;
                                        }
                                    default: break;
                                }

                                ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret, flags);
                            });
                        }
                        else
                        {
                            processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                            {
                                var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
                                Debug.Assert(noteEvent != null, $"Wrong note event type passed to drums note process. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");

                                ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret, defaultFlags);
                            });
                        }
                    }
                }
            };

            foreach (var keyVal in MidIOHelper.PAD_TO_CYMBAL_LOOKUP)
            {
                int pad = (int)keyVal.Key;
                int midiKey = keyVal.Value;

                processFnDict.Add(midiKey, (in EventProcessParams eventProcessParams) =>
                {
                    ProcessNoteOnEventAsFlagToggle(eventProcessParams, Note.Flags.ProDrums_Cymbal, pad);
                });
            }

            return processFnDict;
        }

        static void ProcessNoteOnEventAsNote(in EventProcessParams eventProcessParams, Song.Difficulty diff, int ingameFret, Note.Flags defaultFlags = Note.Flags.None)
        {
            Chart chart;
            if (eventProcessParams.instrument == Song.Instrument.Unrecognised)
            {
                chart = eventProcessParams.currentUnrecognisedChart;
            }
            else
            {
                chart = eventProcessParams.song.GetChart(eventProcessParams.instrument, diff);
            }

            NoteOnEvent noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(noteEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsNote)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var tick = (uint)noteEvent.AbsoluteTime;
            var sus = CalculateSustainLength(eventProcessParams.song, noteEvent);

            Note newNote = new Note(tick, ingameFret, sus, defaultFlags);
            chart.Add(newNote, false);
        }

        static void ProcessNoteOnEventAsStarpower(in EventProcessParams eventProcessParams)
        {
            var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(noteEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsStarpower)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var tick = (uint)noteEvent.AbsoluteTime;
            var sus = CalculateSustainLength(song, noteEvent);

            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).Add(new Starpower(tick, sus), false);
            }
        }

        static void ProcessNoteOnEventAsDrumFill(in EventProcessParams eventProcessParams)
        {
            var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(noteEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsDrumFill)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var tick = (uint)noteEvent.AbsoluteTime;
            var sus = CalculateSustainLength(song, noteEvent);

            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).Add(new Starpower(tick, sus, Starpower.Flags.ProDrums_Activation), false);
            }
        }

        static void ProcessNoteOnEventAsDrumRoll(in EventProcessParams eventProcessParams, DrumRoll.Type type)
        {
            var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(noteEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsDrumRoll)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var tick = (uint)noteEvent.AbsoluteTime;
            var sus = CalculateSustainLength(song, noteEvent);

            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).Add(new DrumRoll(tick, sus, type), false);
            }
        }

        static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, Note.NoteType noteType)
        {
            var flagEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(flagEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsForcedType)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");

            uint startTick = (uint)flagEvent.AbsoluteTime;
            uint endTick = (uint)flagEvent.OffEvent.AbsoluteTime;

            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
            {
                // Delay the actual processing once all the notes are actually in
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                });
            }
        }

        static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, Song.Difficulty difficulty, Note.NoteType noteType)
        {
            var flagEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(flagEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsForcedType)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");

            uint startTick = (uint)flagEvent.AbsoluteTime;
            uint endTick = (uint)flagEvent.OffEvent.AbsoluteTime;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
            {
                ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, difficulty, noteType);
            });
        }

        static void ProcessEventAsForcedTypePostDelay(in EventProcessParams eventProcessParams, uint startTick, uint endTick, Song.Difficulty difficulty, Note.NoteType noteType)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            Chart chart;
            if (instrument != Song.Instrument.Unrecognised)
                chart = song.GetChart(instrument, difficulty);
            else
                chart = eventProcessParams.currentUnrecognisedChart;

            int index, length;
            SongObjectHelper.GetRange(chart.notes, startTick, endTick, out index, out length);

            uint lastChordTick = uint.MaxValue;
            bool expectedForceFailure = true; // Whether or not it is expected that the actual type will not match the expected type
            bool shouldBeForced = false;

            for (int i = index; i < index + length; ++i)
            {
                // Tap marking overrides all other forcing
                if ((chart.notes[i].flags & Note.Flags.Tap) != 0)
                    continue;

                Note note = chart.notes[i];

                // Check if the chord has changed
                if (lastChordTick != note.tick)
                {
                    expectedForceFailure = false;
                    shouldBeForced = false;

                    switch (noteType)
                    {
                        case (Note.NoteType.Strum):
                        {
                            if (!note.isChord && note.isNaturalHopo)
                            {
                                shouldBeForced = true;
                            }
                            break;
                        }

                        case (Note.NoteType.Hopo):
                        {
                            // Forcing consecutive same-fret HOPOs is possible in charts, but we do not allow it
                            // (see RB2's chart of Steely Dan - Bodhisattva)
                            if (!note.isNaturalHopo && note.cannotBeForced)
                            {
                                expectedForceFailure = true;
                            }

                            if (!note.cannotBeForced && (note.isChord || !note.isNaturalHopo))
                            {
                                shouldBeForced = true;
                            }
                            break;
                        }

                        case (Note.NoteType.Tap):
                        {
                            if (!note.IsOpenNote())
                            {
                                note.flags |= Note.Flags.Tap;
                                // Forced flag will be removed shortly after here
                            }
                            else
                            {
                                // Open notes cannot become taps
                                // CH handles this by turning them into open HOPOs, we'll do the same here for consistency with them
                                expectedForceFailure = true;
                                // In the case that consecutive open notes are marked as taps, only the first will become a HOPO
                                if (!note.cannotBeForced && !note.isNaturalHopo)
                                {
                                    shouldBeForced = true;
                                }
                            }
                            break;
                        }

                        default:
                            Debug.Assert(false, $"Unhandled note type {noteType} in .mid forced type processing");
                            continue; // Unhandled
                    }

                    if (shouldBeForced)
                    {
                        note.flags |= Note.Flags.Forced;
                    }
                    else
                    {
                        note.flags &= ~Note.Flags.Forced;
                    }

                    note.ApplyFlagsToChord();
                }

                lastChordTick = note.tick;

                Debug.Assert(note.type == noteType || expectedForceFailure, $"Failed to set forced type! Expected: {noteType}  Actual: {note.type}  Natural HOPO: {note.isNaturalHopo}  Chord: {note.isChord}  Forceable: {!note.cannotBeForced}\non {difficulty} {instrument} at tick {note.tick} ({TimeSpan.FromSeconds(note.time):mm':'ss'.'ff})");
            }
        }

        static uint CalculateSustainLength(Song song, NoteOnEvent noteEvent)
        {
            uint tick = (uint)noteEvent.AbsoluteTime;
            var sus = (uint)(noteEvent.OffEvent.AbsoluteTime - tick);
            int susCutoff = (int)(SongConfig.MIDI_SUSTAIN_CUTOFF_THRESHOLD * song.resolution / SongConfig.STANDARD_BEAT_RESOLUTION); // 1/12th note
            if (sus <= susCutoff)
                sus = 0;

            return sus;
        }

        static void ProcessNoteOnEventAsEvent(EventProcessParams eventProcessParams, string eventStartText, int tickStartOffset, string eventEndText, int tickEndOffset)
        {
            var noteEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(noteEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsEvent)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            uint tick = (uint)(noteEvent.AbsoluteTime + tickStartOffset);            
            uint sus = CalculateSustainLength(song, noteEvent);
            if (sus >= tickEndOffset)
            {
                sus = (uint)(sus + tickEndOffset);
            }

            foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
            {
                Chart chart = song.GetChart(instrument, diff);
                chart.Add(new ChartEvent(tick, eventStartText));
                chart.Add(new ChartEvent(tick + sus, eventEndText));
            }
        }

        static void ProcessNoteOnEventAsFlagToggle(in EventProcessParams eventProcessParams, Note.Flags flags, int individualNoteSpecifier)
        {
            var flagEvent = eventProcessParams.midiEvent as NoteOnEvent;
            Debug.Assert(flagEvent != null, $"Wrong note event type passed to {nameof(ProcessNoteOnEventAsFlagToggle)}. Expected: {typeof(NoteOnEvent)}, Actual: {eventProcessParams.midiEvent.GetType()}");

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
            {
                ProcessNoteOnEventAsFlagTogglePostDelay(processParams, flagEvent, flags, individualNoteSpecifier);
            });
        }

        static void ProcessNoteOnEventAsFlagTogglePostDelay(in EventProcessParams eventProcessParams, NoteOnEvent noteEvent, Note.Flags flags, int individualNoteSpecifier)   // individualNoteSpecifier as -1 to apply to the whole chord
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            uint tick = (uint)noteEvent.AbsoluteTime;
            uint endPos = (uint)(noteEvent.OffEvent.AbsoluteTime - tick);
            --endPos;

            foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
            {
                Chart chart = song.GetChart(instrument, difficulty);

                int index, length;
                SongObjectHelper.GetRange(chart.notes, tick, tick + endPos, out index, out length);

                for (int i = index; i < index + length; ++i)
                {
                    Note note = chart.notes[i];

                    if (individualNoteSpecifier < 0 || note.rawNote == individualNoteSpecifier)
                    {
                        // Toggle flag
                        note.flags ^= flags;
                    }
                }
            }
        }

        static void ProcessTextEventPairAsStarpower(in EventProcessParams eventProcessParams, string startText, string endText, Starpower.Flags flags)
        {
            foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
            {
                var song = eventProcessParams.song;
                var instrument = eventProcessParams.instrument;
                var chart = song.GetChart(instrument, difficulty);

                // Convert start and end events into phrases
                uint? currentStartTick = null;
                for (int i = 0; i < chart.events.Count; ++i)
                {
                    var textEvent = chart.events[i];
                    if (textEvent.eventName == startText)
                    {
                        // Remove text event
                        chart.Remove(textEvent, false);

                        uint startTick = textEvent.tick;
                        // Only one start event can be active at a time
                        if (currentStartTick != null)
                        {
                            Debug.LogError($"A previous start event at tick {currentStartTick.Value} is interrupted by another start event at tick {startTick}!");
                            continue;
                        }

                        currentStartTick = startTick;
                    }
                    else if (textEvent.eventName == endText)
                    {
                        // Remove text event
                        chart.Remove(textEvent, false);

                        uint endTick = textEvent.tick;
                        // Events must pair up
                        if (currentStartTick == null)
                        {
                            Debug.LogError($"End event at tick {endTick} does not have a corresponding start event!");
                            continue;
                        }

                        uint startTick = currentStartTick.GetValueOrDefault();
                        // Current start must occur before the current end
                        if (currentStartTick > textEvent.tick)
                        {
                            Debug.LogError($"Start event at tick {endTick} occurs before end event at {endTick}!");
                            continue;
                        }

                        chart.Add(new Starpower(startTick, endTick - startTick), false);
                        currentStartTick = null;
                    }
                }
            }
        }

        static void ProcessSysExEventPairAsForcedType(in EventProcessParams eventProcessParams, Note.NoteType noteType)
        {
            var startEvent = eventProcessParams.midiEvent as PhaseShiftSysExStart;
            Debug.Assert(startEvent != null, $"Wrong note event type passed to {nameof(ProcessSysExEventPairAsForcedType)}. Expected: {typeof(PhaseShiftSysExStart)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var endEvent = startEvent.endEvent;
            Debug.Assert(endEvent != null, $"No end event supplied to {nameof(ProcessSysExEventPairAsForcedType)}.");

            uint startTick = (uint)startEvent.AbsoluteTime;
            uint endTick = (uint)endEvent.AbsoluteTime;

            if (startEvent.difficulty == MidIOHelper.SYSEX_DIFFICULTY_ALL)
            {
                foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
                {
                    eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                    {
                        ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                    });
                }
            }
            else
            {
                var diff = MidIOHelper.SYSEX_TO_MS_DIFF_LOOKUP[startEvent.difficulty];
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                });
            }
        }

        static void ProcessSysExEventPairAsOpenNoteModifier(in EventProcessParams eventProcessParams)
        {
            var startEvent = eventProcessParams.midiEvent as PhaseShiftSysExStart;
            Debug.Assert(startEvent != null, $"Wrong note event type passed to {nameof(ProcessSysExEventPairAsOpenNoteModifier)}. Expected: {typeof(PhaseShiftSysExStart)}, Actual: {eventProcessParams.midiEvent.GetType()}");
            var endEvent = startEvent.endEvent;
            Debug.Assert(endEvent != null, $"No end event supplied to {nameof(ProcessSysExEventPairAsOpenNoteModifier)}.");

            uint startTick = (uint)startEvent.AbsoluteTime;
            uint endTick = (uint)endEvent.AbsoluteTime;
            // Exclude the last tick of the phrase
            if (endTick > 0)
                --endTick;

            if (startEvent.difficulty == MidIOHelper.SYSEX_DIFFICULTY_ALL)
            {
                foreach (Song.Difficulty diff in EnumX<Song.Difficulty>.Values)
                {
                    eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                    {
                        ProcessEventAsOpenNoteModifierPostDelay(processParams, startTick, endTick, diff);
                    });
                }
            }
            else
            {
                var diff = MidIOHelper.SYSEX_TO_MS_DIFF_LOOKUP[startEvent.difficulty];
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsOpenNoteModifierPostDelay(processParams, startTick, endTick, diff);
                });
            }
        }

        static void ProcessEventAsOpenNoteModifierPostDelay(in EventProcessParams processParams, uint startTick, uint endTick, Song.Difficulty difficulty)
        {
            var instrument = processParams.instrument;
            var gameMode = Song.InstumentToChartGameMode(instrument);
            var song = processParams.song;

            Chart chart;
            if (instrument == Song.Instrument.Unrecognised)
                chart = processParams.currentUnrecognisedChart;
            else
                chart = song.GetChart(instrument, difficulty);

            int index, length;
            SongObjectHelper.GetRange(chart.notes, startTick, endTick, out index, out length);
            for (int i = index; i < index + length; ++i)
            {
                switch (gameMode)
                {
                    case (Chart.GameMode.Guitar):
                        chart.notes[i].guitarFret = Note.GuitarFret.Open;
                        break;

                    // Usually not used, but in the case that it is it should work properly
                    case (Chart.GameMode.GHLGuitar):
                        chart.notes[i].ghliveGuitarFret = Note.GHLiveGuitarFret.Open;
                        break;

                    default:
                        Debug.Assert(false, $"Unhandled game mode for open note modifier: {gameMode} (instrument: {instrument})");
                        break;
                }
            }

            chart.UpdateCache();
        }
    }
}
