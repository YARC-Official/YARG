// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace MoonscraperChartEditor.Song.IO
{
    using NoteEventQueue = List<(NoteEvent note, long tick)>;
    using SysExEventQueue = List<(PhaseShiftSysEx sysex, long tick)>;

    internal static partial class MidReader
    {
        private const int SOLO_END_CORRECTION_OFFSET = -1;

        // true == override existing track, false == discard if already exists
        private static readonly Dictionary<string, bool> TrackOverrides = new()
        {
            { MidIOHelper.GUITAR_TRACK,     true },
            { MidIOHelper.GH1_GUITAR_TRACK, false },

            { MidIOHelper.DRUMS_TRACK,      true },
            { MidIOHelper.DRUMS_TRACK_2,    false },
            { MidIOHelper.DRUMS_REAL_TRACK, false },

            { MidIOHelper.HARMONY_1_TRACK, true },
            { MidIOHelper.HARMONY_2_TRACK, true },
            { MidIOHelper.HARMONY_3_TRACK, true },
            { MidIOHelper.HARMONY_1_TRACK_2, false },
            { MidIOHelper.HARMONY_2_TRACK_2, false },
            { MidIOHelper.HARMONY_3_TRACK_2, false },
        };

        private struct TimedMidiEvent
        {
            public MidiEvent midiEvent;
            public long startTick;
            public long endTick;

            public long length => endTick - startTick;
        }

        private struct EventProcessParams
        {
            public MoonSong song;
            public MoonSong.MoonInstrument instrument;

            public MoonSong.Difficulty? trackDifficulty;

            public ParseSettings settings;
            public TimedMidiEvent timedEvent;

            public Dictionary<int, EventProcessFn> noteProcessMap;
            public Dictionary<int, EventProcessFn> phraseProcessMap;
            public Dictionary<string, ProcessModificationProcessFn> textProcessMap;
            public Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> sysexProcessMap;

            public List<EventProcessFn> forcingProcessList;
            public List<EventProcessFn> sysexProcessList;
            public IReadOnlyList<EventProcessFn> postProcessList;
        }

        public static MoonSong ReadMidi(string path)
        {
            var settings = ParseSettings.Default_Midi;
            return ReadMidi(ref settings, path);
        }

        public static MoonSong ReadMidi(Stream stream)
        {
            var settings = ParseSettings.Default_Midi;
            return ReadMidi(ref settings, stream);
        }

        public static MoonSong ReadMidi(MidiFile midi)
        {
            var settings = ParseSettings.Default_Midi;
            return ReadMidi(ref settings, midi);
        }

        public static MoonSong ReadMidi(ref ParseSettings settings, string path)
        {
            return ReadMidi(ref settings, MidFileLoader.LoadMidiFile(path));
        }

        public static MoonSong ReadMidi(ref ParseSettings settings, Stream stream)
        {
            return ReadMidi(ref settings, MidFileLoader.LoadMidiFile(stream));
        }

        public static MoonSong ReadMidi(ref ParseSettings settings, MidiFile midi)
        {
            if (midi.Chunks == null || midi.Chunks.Count < 1)
                throw new InvalidOperationException("MIDI file has no tracks, unable to parse.");

            if (midi.TimeDivision is not TicksPerQuarterNoteTimeDivision ticks)
                throw new InvalidOperationException("MIDI file has no beat resolution set!");

            var song = new MoonSong((uint)ticks.TicksPerQuarterNote);

            // Apply settings
            song.hopoThreshold = settings.HopoThreshold > ParseSettings.SETTING_DEFAULT
                // +1 for a small bit of leniency
                ? (uint)settings.HopoThreshold + 1 
                : (song.resolution / 3) + 1;

            if (settings.SustainCutoffThreshold <= ParseSettings.SETTING_DEFAULT)
            {
                settings.SustainCutoffThreshold = (song.resolution / 3) + 1;
            }
            else if (settings.SustainCutoffThreshold == 0)
            {
                // Limit minimum cutoff to 1 tick, non - sustain notes created by charting programs are 1 tick
                settings.SustainCutoffThreshold = 1;
            }

            // Read all bpm data in first. This will also allow song.TimeToTick to function properly.
            ReadSync(midi.GetTempoMap(), song);

            foreach (var track in midi.GetTrackChunks())
            {
                if (track == null || track.Events.Count < 1)
                {
                    YargLogger.LogTrace("Encountered an empty MIDI track!");
                    continue;
                }

                string trackName = track.GetTrackName();
                switch (trackName)
                {
                    case MidIOHelper.BEAT_TRACK:
                        ReadSongBeats(track, song);
                        break;

                    case MidIOHelper.EVENTS_TRACK:
                        ReadSongGlobalEvents(track, song);
                        break;

                    case MidIOHelper.VENUE_TRACK:
                        ReadVenueEvents(track, song);
                        break;

                    case MidIOHelper.PRO_KEYS_EXPERT:
                        ReadNotes(ref settings, track, song, MoonSong.MoonInstrument.ProKeys, MoonSong.Difficulty.Expert);
                        break;
                    case MidIOHelper.PRO_KEYS_HARD:
                        ReadNotes(ref settings, track, song, MoonSong.MoonInstrument.ProKeys, MoonSong.Difficulty.Hard);
                        break;
                    case MidIOHelper.PRO_KEYS_MEDIUM:
                        ReadNotes(ref settings, track, song, MoonSong.MoonInstrument.ProKeys, MoonSong.Difficulty.Medium);
                        break;
                    case MidIOHelper.PRO_KEYS_EASY:
                        ReadNotes(ref settings, track, song, MoonSong.MoonInstrument.ProKeys, MoonSong.Difficulty.Easy);
                        break;

                    case MidIOHelper.VOCALS_TRACK:
                        // Parse lyrics to global track, and then parse as an instrument
                        ReadTextEventsIntoGlobalEventsAsLyrics(track, song);
                        goto default;

                    default:
                        MoonSong.MoonInstrument instrument;
                        if (!MidIOHelper.TrackNameToInstrumentMap.TryGetValue(trackName, out instrument))
                        {
                            // Ignore unrecognized tracks
                            YargLogger.LogFormatTrace("Skipping unrecognized track {0}", trackName);
                            continue;
                        }
                        else if (song.ChartExistsForInstrument(instrument))
                        {
                            if (!TrackOverrides.TryGetValue(trackName, out bool overwrite) || !overwrite)
                                continue;

                            // Overwrite existing track
                            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
                            {
                                var chart = song.GetChart(instrument, difficulty);
                                chart.Clear();
                            }
                        }

                        YargLogger.LogFormatTrace("Loading MIDI track {0}", trackName);
                        ReadNotes(ref settings, track, song, instrument);
                        break;
                }
            }

            return song;
        }

        private static void ReadSync(TempoMap tempoMap, MoonSong song)
        {
            YargLogger.LogTrace("Reading sync track");

            foreach (var tempo in tempoMap.GetTempoChanges())
            {
                uint tempoTick = (uint) tempo.Time;
                song.Add(new TempoChange((float) tempo.Value.BeatsPerMinute,
                    // This is valid since we are guaranteed to have at least one tempo event at all times
                    song.TickToTime(tempoTick, song.syncTrack.Tempos[^1]), tempoTick));
            }

            var tempoTracker = new ChartEventTickTracker<TempoChange>(song.syncTrack.Tempos);
            foreach (var timesig in tempoMap.GetTimeSignatureChanges())
            {
                uint tsTick = (uint) timesig.Time;
                tempoTracker.Update(tsTick);
                song.Add(new TimeSignatureChange((uint) timesig.Value.Numerator, (uint) timesig.Value.Denominator,
                    song.TickToTime(tsTick, tempoTracker.Current!), tsTick));
            }
        }

        private static void ReadSongBeats(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            YargLogger.LogTrace("Reading beat track");
            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (trackEvent is NoteEvent note && note.EventType == MidiEventType.NoteOn)
                {
                    BeatlineType beatType;
                    switch ((byte)note.NoteNumber)
                    {
                        case MidIOHelper.BEAT_STRONG:
                            beatType = BeatlineType.Measure;
                            break;
                        case MidIOHelper.BEAT_WEAK:
                            beatType = BeatlineType.Strong;
                            break;
                        default:
                            continue;
                    }

                    song.Add(new Beatline(beatType, song.TickToTime((uint) absoluteTime), (uint)absoluteTime));
                }
            }
        }

        private static void ReadSongGlobalEvents(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            YargLogger.LogTrace("Reading global events");
            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (MidIOHelper.IsTextEvent(trackEvent, out var text))
                {
                    // Get event text
                    var eventText = TextEvents.NormalizeTextEvent(text.Text);

                    // Check for section events
                    if (TextEvents.TryParseSectionEvent(eventText, out var sectionName))
                    {
                        song.sections.Add(new MoonText(sectionName.ToString(), (uint)absoluteTime));
                    }
                    else
                    {
                        song.events.Add(new MoonText(eventText.ToString(), (uint)absoluteTime));
                    }
                }
            }
        }

        private static void ReadTextEventsIntoGlobalEventsAsLyrics(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            YargLogger.LogTrace("Reading global lyrics");
            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (MidIOHelper.IsTextEvent(trackEvent, out var text) && !text.Text.Contains('['))
                {
                    string lyricEvent = TextEvents.LYRIC_PREFIX_WITH_SPACE + text.Text;
                    song.events.Add(new MoonText(lyricEvent, (uint)absoluteTime));
                }
                else if (trackEvent is NoteEvent note && (byte)note.NoteNumber is MidIOHelper.LYRICS_PHRASE_1 or MidIOHelper.LYRICS_PHRASE_2)
                {
                    if (note.EventType == MidiEventType.NoteOn)
                        song.events.Add(new MoonText(TextEvents.LYRIC_PHRASE_START, (uint)absoluteTime));
                    else if (note.EventType == MidiEventType.NoteOff)
                        song.events.Add(new MoonText(TextEvents.LYRIC_PHRASE_END, (uint)absoluteTime));
                }
            }
        }

        private static void ReadVenueEvents(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            YargLogger.LogTrace("Reading venue track");

            var unpairedNoteQueue = new NoteEventQueue();

            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (trackEvent is NoteEvent note)
                {
                    if (note.EventType == MidiEventType.NoteOn)
                    {
                        // Check for duplicates
                        if (TryFindMatchingNote(unpairedNoteQueue, note, out _, out _, out _))
                            YargLogger.LogFormatWarning("Found duplicate note on at tick {0}!", absoluteTime);
                        else
                            unpairedNoteQueue.Add((note, absoluteTime));
                    }
                    else if (note.EventType == MidiEventType.NoteOff)
                    {
                        // Find starting note
                        if (!TryFindMatchingNote(unpairedNoteQueue, note, out var noteStart, out long startTick, out int startIndex))
                        {
                            YargLogger.LogFormatWarning("Found note off with no corresponding note on at tick {0}!", absoluteTime);
                            return;
                        }
                        unpairedNoteQueue.RemoveAt(startIndex);

                        // Turn note into event data
                        if (!MidIOHelper.VENUE_NOTE_LOOKUP.TryGetValue((byte)noteStart.NoteNumber, out var eventData))
                            continue;

                        // Add the event
                        song.venue.Add(new MoonVenue(eventData.type, eventData.text, (uint)startTick, (uint)(startTick - absoluteTime)));
                    }
                }
                else if (MidIOHelper.IsTextEvent(trackEvent, out var text))
                {
                    string eventText = TextEvents.NormalizeTextEvent(text.Text).ToString();

                    // Get new representation of the event
                    if (VenueLookup.VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(eventText, out var eventData))
                    {
                        song.venue.Add(new MoonVenue(eventData.type, eventData.text, (uint)absoluteTime));
                    }
                    else
                    {
                        // Events that need special matching
                        bool matched = false;
                        foreach (var (regex, (lookup, type, defaultValue)) in MidIOHelper.VENUE_EVENT_REGEX_TO_LOOKUP)
                        {
                            if (regex.Match(eventText) is not { Success: true } match)
                                continue;

                            // Get new representation of the event
                            if (!lookup.TryGetValue(match.Groups[1].Value, out string converted))
                            {
                                if (string.IsNullOrEmpty(defaultValue))
                                    continue;
                                converted = defaultValue;
                            }

                            matched = true;
                            song.venue.Add(new MoonVenue(type, converted, (uint)absoluteTime));
                            break;
                        }

                        // Unknown events
                        if (!matched)
                            song.venue.Add(new MoonVenue(VenueLookup.Type.Unknown, eventText, (uint)absoluteTime));
                    }
                }
            }
        }

        private static void ReadNotes(ref ParseSettings settings, TrackChunk track, MoonSong song,
            MoonSong.MoonInstrument instrument, MoonSong.Difficulty? trackDifficulty = null)
        {
            if (track == null || track.Events.Count < 1)
            {
                if (trackDifficulty is {} difficulty)
                    YargLogger.LogFormatTrace("Skipping empty track for {0} {1}", difficulty, instrument);
                else
                    YargLogger.LogFormatTrace("Skipping empty track for instrument {0}", instrument);
                return;
            }

            var unpairedNoteQueue = new NoteEventQueue();
            var unpairedSysexQueue = new SysExEventQueue();

            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);

            var processParams = new EventProcessParams()
            {
                song = song,
                instrument = instrument,
                trackDifficulty = trackDifficulty,
                settings = settings,
                noteProcessMap = GetNoteProcessDict(gameMode),
                phraseProcessMap = GetPhraseProcessDict(settings.StarPowerNote, gameMode),
                textProcessMap = GetTextEventProcessDict(gameMode),
                sysexProcessMap = GetSysExEventProcessDict(gameMode),
                forcingProcessList = new(),
                sysexProcessList = new(),
                postProcessList = GetPostProcessList(gameMode),
            };

            // Load all the notes
            long absoluteTick = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTick += trackEvent.DeltaTime;

                processParams.timedEvent = new TimedMidiEvent()
                {
                    midiEvent = trackEvent,
                    startTick = absoluteTick
                };

                if (trackEvent is NoteEvent note)
                {
                    ProcessNoteEvent(ref processParams, unpairedNoteQueue, note, absoluteTick);
                }
                else if (MidIOHelper.IsTextEvent(trackEvent, out var text))
                {
                    ProcessTextEvent(ref processParams, text, absoluteTick);
                }
                else if (trackEvent is SysExEvent sysex)
                {
                    ProcessSysExEvent(ref processParams, unpairedSysexQueue, sysex, absoluteTick);
                }
            }

            YargLogger.Assert(unpairedNoteQueue.Count == 0);
            YargLogger.Assert(unpairedSysexQueue.Count == 0);

            // Apply SysEx events first
            // These are separate to prevent forcing issues on open notes marked via SysEx
            foreach (var process in processParams.sysexProcessList)
            {
                process(ref processParams);
            }

            // Apply forcing events
            foreach (var process in processParams.forcingProcessList)
            {
                process(ref processParams);
            }

            // Apply post-processing
            // Also separate, to ensure that everything is in before post-processing
            foreach (var process in processParams.postProcessList)
            {
                process(ref processParams);
            }

            // If this specific track does not have a difficulty assigned to it, clear all difficulties
            if (trackDifficulty is null)
            {
                foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
                {
                    song.GetChart(instrument, difficulty).notes.TrimExcess();
                }
            }
            else
            {
                song.GetChart(instrument, trackDifficulty.Value).notes.TrimExcess();
            }

            settings = processParams.settings;
        }

        private static void ProcessNoteEvent(ref EventProcessParams processParams, NoteEventQueue unpairedNotes,
            NoteEvent note, long absoluteTick)
        {
            if (note.EventType == MidiEventType.NoteOn)
            {
                // Check for duplicates
                if (TryFindMatchingNote(unpairedNotes, note, out _, out _, out _))
                    YargLogger.LogFormatWarning("Found duplicate note on at tick {0}!", absoluteTick);
                else
                    unpairedNotes.Add((note, absoluteTick));
            }
            else if (note.EventType == MidiEventType.NoteOff)
            {
                if (!TryFindMatchingNote(unpairedNotes, note, out var noteStart, out long startTick, out int startIndex))
                {
                    YargLogger.LogFormatWarning("Found note off with no corresponding note on at tick {0}!", absoluteTick);
                    return;
                }
                unpairedNotes.RemoveAt(startIndex);

                processParams.timedEvent.midiEvent = noteStart;
                processParams.timedEvent.startTick = startTick;
                processParams.timedEvent.endTick = absoluteTick;

                if (processParams.noteProcessMap.TryGetValue(noteStart.NoteNumber, out var processFn) ||
                    processParams.phraseProcessMap.TryGetValue(noteStart.NoteNumber, out processFn))
                {
                    processFn(ref processParams);
                }
            }
        }

        private static void ProcessTextEvent(ref EventProcessParams processParams, BaseTextEvent text, long absoluteTick)
        {
            uint tick = (uint)absoluteTick;

            string eventText = TextEvents.NormalizeTextEvent(text.Text, out bool strippedBrackets).ToString();
            if (processParams.textProcessMap.TryGetValue(eventText, out var processFn))
            {
                // This text event affects parsing of the .mid file, run its function and don't parse it into the chart
                processFn(ref processParams);
                return;
            }
            // No brackets to strip off, on vocals this is most likely a lyric event
            else if (!strippedBrackets && MoonSong.InstrumentToChartGameMode(processParams.instrument) is MoonChart.GameMode.Vocals)
            {
                eventText = TextEvents.LYRIC_PREFIX_WITH_SPACE + eventText;
            }

            // Copy text event to all difficulties
            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                processParams.song.GetChart(processParams.instrument, difficulty).events.Add(new MoonText(eventText, tick));
            }
        }

        private static void ProcessSysExEvent(ref EventProcessParams processParams, SysExEventQueue unpairedSysex,
            SysExEvent sysex, long absoluteTick)
        {
            if (!PhaseShiftSysEx.TryParse(sysex, out var psEvent))
            {
                // SysEx event is not a Phase Shift SysEx event
                YargLogger.LogFormatWarning("Encountered unknown SysEx event at tick {0}: {1}",
                    absoluteTick, new HexBytesFormat(sysex.Data));
                return;
            }

            if (psEvent.type != PhaseShiftSysEx.Type.Phrase)
            {
                YargLogger.LogFormatWarning("Encountered unknown Phase Shift SysEx event type {0} at tick {1}!",
                    psEvent.type, absoluteTick);
                return;
            }

            if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.Start)
            {
                // Check for duplicates
                if (TryFindMatchingSysEx(unpairedSysex, psEvent, out _, out _, out _))
                    YargLogger.LogFormatWarning("Found duplicate SysEx start event at tick {0}!", absoluteTick);
                else
                    unpairedSysex.Add((psEvent, absoluteTick));
            }
            else if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.End)
            {
                if (!TryFindMatchingSysEx(unpairedSysex, psEvent, out var sysexStart, out long startTick, out int startIndex))
                {
                    YargLogger.LogFormatWarning("Found PS SysEx end with no corresponding start at tick {0}!", absoluteTick);
                    return;
                }
                unpairedSysex.RemoveAt(startIndex);

                processParams.timedEvent.midiEvent = sysexStart;
                processParams.timedEvent.startTick = startTick;
                processParams.timedEvent.endTick = absoluteTick;

                if (processParams.sysexProcessMap.TryGetValue(psEvent.phraseCode, out var processFn))
                {
                    processFn(ref processParams);
                }
            }
        }

        private static bool TryFindMatchingNote(NoteEventQueue unpairedNotes, NoteEvent noteToMatch,
            [NotNullWhen(true)] out NoteEvent? matchingNote, out long matchTick, out int matchIndex)
        {
            for (int i = 0; i < unpairedNotes.Count; i++)
            {
                var queued = unpairedNotes[i];
                if (queued.note.NoteNumber == noteToMatch.NoteNumber && queued.note.Channel == noteToMatch.Channel)
                {
                    (matchingNote, matchTick) = queued;
                    matchIndex = i;
                    return true;
                }
            }

            matchingNote = null;
            matchTick = -1;
            matchIndex = -1;
            return false;
        }

        private static bool TryFindMatchingSysEx(SysExEventQueue unpairedSysex, PhaseShiftSysEx sysexToMatch,
            [NotNullWhen(true)] out PhaseShiftSysEx? matchingSysex, out long matchTick, out int matchIndex)
        {
            for (int i = 0; i < unpairedSysex.Count; i++)
            {
                var queued = unpairedSysex[i];
                if (queued.sysex.MatchesWith(sysexToMatch))
                {
                    (matchingSysex, matchTick) = queued;
                    matchIndex = i;
                    return true;
                }
            }

            matchingSysex = null;
            matchTick = -1;
            matchIndex = -1;
            return false;
        }

        private static bool ContainsTextEvent(List<MoonText> events, string text)
        {
            foreach (var textEvent in events)
            {
                if (textEvent.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ProcessNoteOnEventAsNote(ref EventProcessParams eventProcessParams, MoonSong.Difficulty diff,
            int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None, bool sustainCutoff = true)
        {
            var chart = eventProcessParams.song.GetChart(eventProcessParams.instrument, diff);

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = (uint)timedEvent.length;
            if (sustainCutoff && sus < eventProcessParams.settings.SustainCutoffThreshold)
            {
                sus = 0;
            }

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            if (chart.notes.Capacity == 0)
                chart.notes.Capacity = 5000;

            MoonObjectHelper.OrderedInsertFromBack(newMoonNote, chart.notes);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(ref EventProcessParams eventProcessParams,
            MoonPhrase.Type type, MoonSong.Difficulty? difficulty = null)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = (uint)timedEvent.length;

            if (difficulty is null)
            {
                foreach (var diff in EnumExtensions<MoonSong.Difficulty>.Values)
                {
                    MoonObjectHelper.OrderedInsertFromBack(new MoonPhrase(tick, sus, type),
                        song.GetChart(instrument, diff).specialPhrases);
                }
            }
            else
            {
                MoonObjectHelper.OrderedInsertFromBack(new MoonPhrase(tick, sus, type),
                    song.GetChart(instrument, difficulty.Value).specialPhrases);
            }
        }

        private static void ProcessNoteOnEventAsGuitarForcedType(ref EventProcessParams eventProcessParams, MoonNote.MoonNoteType noteType)
        {
            foreach (var diff in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                ProcessNoteOnEventAsGuitarForcedType(ref eventProcessParams, diff, noteType);
            }
        }

        private static void ProcessNoteOnEventAsGuitarForcedType(ref EventProcessParams eventProcessParams, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType noteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;
            // Exclude the last tick of the phrase
            if (endTick > startTick)
                --endTick;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.forcingProcessList.Add((ref EventProcessParams processParams) =>
            {
                ProcessEventAsGuitarForcedTypePostDelay(ref processParams, startTick, endTick, difficulty, noteType);
            });
        }

        private static void ProcessEventAsGuitarForcedTypePostDelay(ref EventProcessParams eventProcessParams, uint startTick, uint endTick, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType noteType)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;
            var chart = song.GetChart(instrument, difficulty);
            var gameMode = chart.gameMode;

            // Drums force notes are handled by ProcessNoteOnEventAsFlagToggle
            if (gameMode is MoonChart.GameMode.Drums)
                return;

            MoonObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);

            for (int i = index; i < index + length; ++i)
            {
                var note = chart.notes[i];
                var newType = noteType; // The requested type might not be able to be marked for this note

                // Tap marking overrides all other forcing
                if ((note.flags & MoonNote.Flags.Tap) != 0)
                    continue;

                switch (newType)
                {
                    case MoonNote.MoonNoteType.Strum:
                        note.flags |= MoonNote.Flags.Forced_Strum;
                        note.flags &= ~MoonNote.Flags.Forced_Hopo;
                        if (!note.isChord && note.IsNaturalHopo(song.hopoThreshold))
                            note.flags |= MoonNote.Flags.Forced;
                        else
                            note.flags &= ~MoonNote.Flags.Forced;
                        break;

                    case MoonNote.MoonNoteType.Hopo:
                        note.flags |= MoonNote.Flags.Forced_Hopo;
                        note.flags &= ~MoonNote.Flags.Forced_Strum;
                        if (note.isChord || !note.IsNaturalHopo(song.hopoThreshold))
                            note.flags |= MoonNote.Flags.Forced;
                        else
                            note.flags &= ~MoonNote.Flags.Forced;
                        break;

                    case MoonNote.MoonNoteType.Tap:
                        note.flags |= MoonNote.Flags.Tap;
                        note.flags &= ~MoonNote.Flags.Forced;
                        break;

                    default:
                        YargLogger.FailFormat("Unhandled note type {0} in .mid forced type processing!", newType);
                        continue;
                }

                var finalType = note.GetGuitarNoteType(song.hopoThreshold);
                YargLogger.Assert(finalType == newType);
            }
        }

        private static void ProcessNoteOnEventAsFlagToggle(ref EventProcessParams eventProcessParams, MoonNote.Flags flags, int individualNoteSpecifier)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;
            // Exclude the last tick of the phrase
            if (endTick > startTick)
                --endTick;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.forcingProcessList.Add((ref EventProcessParams processParams) =>
            {
                ProcessNoteOnEventAsFlagTogglePostDelay(ref processParams, startTick, endTick, flags, individualNoteSpecifier);
            });
        }

        private static void ProcessNoteOnEventAsFlagTogglePostDelay(ref EventProcessParams eventProcessParams, uint startTick, uint endTick, MoonNote.Flags flags, int individualNoteSpecifier)   // individualNoteSpecifier as -1 to apply to the whole chord
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            foreach (var difficulty in EnumExtensions<MoonSong.Difficulty>.Values)
            {
                var chart = song.GetChart(instrument, difficulty);

                MoonObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);

                for (int i = index; i < index + length; ++i)
                {
                    var note = chart.notes[i];

                    if (individualNoteSpecifier < 0 || note.rawNote == individualNoteSpecifier)
                    {
                        // Toggle flag
                        note.flags ^= flags;
                    }
                }
            }
        }

        private static void ProcessSysExEventPairAsGuitarForcedType(ref EventProcessParams eventProcessParams, MoonNote.MoonNoteType noteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            if (eventProcessParams.timedEvent.midiEvent is not PhaseShiftSysEx startEvent)
            {
                YargLogger.FailFormat("Wrong note event type! Expected: {0}, Actual: {1}",
                    typeof(PhaseShiftSysEx), eventProcessParams.timedEvent.midiEvent.GetType());
                return;
            }

            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;
            // Tap note phrases do *not* exclude the last tick, based on both Phase Shift and Clone Hero
            // if (endTick > startTick)
            //     --endTick;

            if (startEvent.difficulty == PhaseShiftSysEx.Difficulty.All)
            {
                foreach (var diff in EnumExtensions<MoonSong.Difficulty>.Values)
                {
                    eventProcessParams.sysexProcessList.Add((ref EventProcessParams processParams) =>
                    {
                        ProcessEventAsGuitarForcedTypePostDelay(ref processParams, startTick, endTick, diff, noteType);
                    });
                }
            }
            else
            {
                var diff = PhaseShiftSysEx.SysExDiffToMsDiff[startEvent.difficulty];
                eventProcessParams.sysexProcessList.Add((ref EventProcessParams processParams) =>
                {
                    ProcessEventAsGuitarForcedTypePostDelay(ref processParams, startTick, endTick, diff, noteType);
                });
            }
        }

        private static void ProcessSysExEventPairAsOpenNoteModifier(ref EventProcessParams eventProcessParams)
        {
            var timedEvent = eventProcessParams.timedEvent;
            if (eventProcessParams.timedEvent.midiEvent is not PhaseShiftSysEx startEvent)
            {
                YargLogger.FailFormat("Wrong note event type! Expected: {0}, Actual: {1}",
                    typeof(PhaseShiftSysEx), eventProcessParams.timedEvent.midiEvent.GetType());
                return;
            }

            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;
            // Open note phrases *do* exclude the last tick, based on both Phase Shift and Clone Hero
            if (endTick > startTick)
                --endTick;

            if (startEvent.difficulty == PhaseShiftSysEx.Difficulty.All)
            {
                foreach (var diff in EnumExtensions<MoonSong.Difficulty>.Values)
                {
                    eventProcessParams.sysexProcessList.Add((ref EventProcessParams processParams) =>
                    {
                        ProcessEventAsOpenNoteModifierPostDelay(ref processParams, startTick, endTick, diff);
                    });
                }
            }
            else
            {
                var diff = PhaseShiftSysEx.SysExDiffToMsDiff[startEvent.difficulty];
                eventProcessParams.sysexProcessList.Add((ref EventProcessParams processParams) =>
                {
                    ProcessEventAsOpenNoteModifierPostDelay(ref processParams, startTick, endTick, diff);
                });
            }
        }

        private static void ProcessEventAsOpenNoteModifierPostDelay(ref EventProcessParams processParams, uint startTick, uint endTick, MoonSong.Difficulty difficulty)
        {
            var instrument = processParams.instrument;
            var song = processParams.song;
            var chart = song.GetChart(instrument, difficulty);
            var gameMode = chart.gameMode;

            MoonObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);
            for (int i = index; i < index + length; ++i)
            {
                switch (gameMode)
                {
                    case MoonChart.GameMode.Guitar:
                        chart.notes[i].guitarFret = MoonNote.GuitarFret.Open;
                        break;

                    // Usually not used, but in the case that it is it should work properly
                    case MoonChart.GameMode.GHLGuitar:
                        chart.notes[i].ghliveGuitarFret = MoonNote.GHLiveGuitarFret.Open;
                        break;

                    default:
                        YargLogger.FailFormat("Unhandled game mode {0} (instrument: {1}) for open note modifier!)",
                            gameMode, instrument);
                        break;
                }
            }
        }
    }
}
