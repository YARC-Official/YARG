using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static MidIOHelper;
    using static ParseBehaviorTests;

    using MidiEventList = List<(long absoluteTick, MidiEvent midiEvent)>;

    public class MidiParseBehaviorTests
    {
        private const uint SUSTAIN_CUTOFF_THRESHOLD = RESOLUTION / 3;
        private const uint HOPO_THRESHOLD = (RESOLUTION / 3) + 1;

        private static readonly Dictionary<MoonInstrument, string> InstrumentToNameLookup = new()
        {
            { MoonInstrument.Guitar,       GUITAR_TRACK },
            { MoonInstrument.GuitarCoop,   GUITAR_COOP_TRACK },
            { MoonInstrument.Bass,         BASS_TRACK },
            { MoonInstrument.Rhythm,       RHYTHM_TRACK },
            { MoonInstrument.Keys,         KEYS_TRACK },
            { MoonInstrument.Drums,        DRUMS_TRACK },

            { MoonInstrument.GHLiveGuitar, GHL_GUITAR_TRACK },
            { MoonInstrument.GHLiveBass,   GHL_BASS_TRACK },
            { MoonInstrument.GHLiveRhythm, GHL_RHYTHM_TRACK },
            { MoonInstrument.GHLiveCoop,   GHL_GUITAR_COOP_TRACK },

            { MoonInstrument.ProGuitar_17Fret, PRO_GUITAR_17_FRET_TRACK },
            { MoonInstrument.ProGuitar_22Fret, PRO_GUITAR_22_FRET_TRACK },
            { MoonInstrument.ProBass_17Fret,   PRO_BASS_17_FRET_TRACK },
            { MoonInstrument.ProBass_22Fret,   PRO_BASS_22_FRET_TRACK },

            { MoonInstrument.Vocals,   VOCALS_TRACK },
            { MoonInstrument.Harmony1, HARMONY_1_TRACK },
            { MoonInstrument.Harmony2, HARMONY_2_TRACK },
            { MoonInstrument.Harmony3, HARMONY_3_TRACK },
        };

#pragma warning disable IDE0230 // Use UTF-8 string literal
        private static readonly Dictionary<int, int> GuitarNoteOffsetLookup = new()
        {
            { (int)GuitarFret.Open,   -1 },
            { (int)GuitarFret.Green,  0 },
            { (int)GuitarFret.Red,    1 },
            { (int)GuitarFret.Yellow, 2 },
            { (int)GuitarFret.Blue,   3 },
            { (int)GuitarFret.Orange, 4 },
        };

        private static readonly Dictionary<MoonNoteType, int> GuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  5 },
            { MoonNoteType.Strum, 6 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> GuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,      new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,           new[] { SOLO_NOTE } },
            { MoonPhrase.Type.Versus_Player1, new[] { VERSUS_PHRASE_PLAYER_1 } },
            { MoonPhrase.Type.Versus_Player2, new[] { VERSUS_PHRASE_PLAYER_2 } },
            { MoonPhrase.Type.TremoloLane,    new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,      new[] { TRILL_LANE_NOTE } },
        };

        private static readonly Dictionary<int, int> GhlGuitarNoteOffsetLookup = new()
        {
            { (int)GHLiveGuitarFret.Open,   0 },
            { (int)GHLiveGuitarFret.Black1, 4 },
            { (int)GHLiveGuitarFret.Black2, 5 },
            { (int)GHLiveGuitarFret.Black3, 6 },
            { (int)GHLiveGuitarFret.White1, 1 },
            { (int)GHLiveGuitarFret.White2, 2 },
            { (int)GHLiveGuitarFret.White3, 3 },
        };

        private static readonly Dictionary<MoonNoteType, int> GhlGuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  7 },
            { MoonNoteType.Strum, 8 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> GhlGuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower, new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,      new[] { SOLO_NOTE } },
        };

        private static readonly Dictionary<int, int> ProGuitarNoteOffsetLookup = new()
        {
            { (int)ProGuitarString.Red,    0 },
            { (int)ProGuitarString.Green,  1 },
            { (int)ProGuitarString.Orange, 2 },
            { (int)ProGuitarString.Blue,   3 },
            { (int)ProGuitarString.Yellow, 4 },
            { (int)ProGuitarString.Purple, 5 },
        };

        private static readonly Dictionary<MoonNoteType, int> ProGuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  6 },
        };

        private static readonly Dictionary<Flags, byte> ProGuitarChannelFlagLookup =
            PRO_GUITAR_CHANNEL_FLAG_LOOKUP.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<MoonPhrase.Type, byte[]> ProGuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,   new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,        new[] { SOLO_NOTE_PRO_GUITAR } },
            { MoonPhrase.Type.TremoloLane, new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,   new[] { TRILL_LANE_NOTE } },
        };

        private static readonly Dictionary<int, int> DrumsNoteOffsetLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> DrumsSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,           new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,                new[] { SOLO_NOTE } },
            { MoonPhrase.Type.Versus_Player1,      new[] { VERSUS_PHRASE_PLAYER_1 } },
            { MoonPhrase.Type.Versus_Player2,      new[] { VERSUS_PHRASE_PLAYER_2 } },
            { MoonPhrase.Type.TremoloLane,         new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,           new[] { TRILL_LANE_NOTE } },
            { MoonPhrase.Type.ProDrums_Activation, new[] { DRUM_FILL_NOTE_0, DRUM_FILL_NOTE_1, DRUM_FILL_NOTE_2, DRUM_FILL_NOTE_3, DRUM_FILL_NOTE_4 } },
        };

        private static readonly Dictionary<int, int> VocalsNoteOffsetLookup = BuildVocalsNoteLookup();

        private static Dictionary<int, int> BuildVocalsNoteLookup()
        {
            var lookup = new Dictionary<int, int>()
            {
                { 0,   PERCUSSION_NOTE},
            };

            for (int i = VOCALS_RANGE_START; i <= VOCALS_RANGE_END; i++)
            {
                lookup.Add(i, i);
            }

            return lookup;
        }

        private static readonly Dictionary<Difficulty, int> VocalsDifficultyStartOffsetLookup = new()
        {
            { Difficulty.Expert, 0 },
            { Difficulty.Hard,   0 },
            { Difficulty.Medium, 0 },
            { Difficulty.Easy,   0 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> VocalsSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,      new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Versus_Player1, new[] { LYRICS_PHRASE_1 } },
            { MoonPhrase.Type.Versus_Player2, new[] { LYRICS_PHRASE_2 } },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, int>> InstrumentNoteOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteOffsetLookup },
            { GameMode.Drums,     DrumsNoteOffsetLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteOffsetLookup },
            { GameMode.ProGuitar, ProGuitarNoteOffsetLookup },
            { GameMode.Vocals,    VocalsNoteOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonNoteType, int>> InstrumentForceOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarForceOffsetLookup },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, GhlGuitarForceOffsetLookup },
            { GameMode.ProGuitar, ProGuitarForceOffsetLookup },
            { GameMode.Vocals,     new() },
        };

        private static readonly Dictionary<GameMode, Dictionary<Flags, byte>> InstrumentChannelFlagLookup = new()
        {
            { GameMode.Guitar,    new() },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, new() },
            { GameMode.ProGuitar, ProGuitarChannelFlagLookup },
            { GameMode.Vocals,     new() },
        };

        private static readonly Dictionary<GameMode, Dictionary<Difficulty, int>> InstrumentDifficultyStartLookup = new()
        {
            { GameMode.Guitar,    GUITAR_DIFF_START_LOOKUP },
            { GameMode.Drums,     DRUMS_DIFF_START_LOOKUP },
            { GameMode.GHLGuitar, GHL_GUITAR_DIFF_START_LOOKUP },
            { GameMode.ProGuitar, PRO_GUITAR_DIFF_START_LOOKUP },
            { GameMode.Vocals,    VocalsDifficultyStartOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonPhrase.Type, byte[]>> InstrumentSpecialPhraseLookup = new()
        {
            { GameMode.Guitar,    GuitarSpecialPhraseLookup },
            { GameMode.Drums,     DrumsSpecialPhraseLookup },
            { GameMode.GHLGuitar, GhlGuitarSpecialPhraseLookup },
            { GameMode.ProGuitar, ProGuitarSpecialPhraseLookup },
            { GameMode.Vocals,    VocalsSpecialPhraseLookup },
        };
#pragma warning restore IDE0230

        // Because SevenBitNumber andFourBitNumber have no implicit operators for taking in bytes
        private static SevenBitNumber S(byte number) => (SevenBitNumber)number;
        private static FourBitNumber F(byte number) => (FourBitNumber)number;

        private static TrackChunk GenerateSyncChunk(MoonSong sourceSong)
        {
            var timedEvents = new MidiEventList();

            var syncTrack = sourceSong.syncTrack;

            // Indexing the separate lists is the only way to
            // 1: Not allocate more space for a combined list, and
            // 2: Not rely on polymorphic queries
            int timeSigIndex = 0;
            int bpmIndex = 0;
            while (timeSigIndex < syncTrack.TimeSignatures.Count ||
                   bpmIndex < syncTrack.Tempos.Count)
            {
                // Generate in this order: time sig, bpm
                while (timeSigIndex < syncTrack.TimeSignatures.Count &&
                    // Time sig comes before or at the same time as a bpm
                    (bpmIndex == syncTrack.Tempos.Count || syncTrack.TimeSignatures[timeSigIndex].Tick <= syncTrack.Tempos[bpmIndex].Tick))
                {
                    var ts = syncTrack.TimeSignatures[timeSigIndex++];
                    timedEvents.Add((ts.Tick, new TimeSignatureEvent((byte) ts.Numerator, (byte) ts.Denominator)));
                }

                while (bpmIndex < syncTrack.Tempos.Count &&
                    // Bpm comes before a time sig (equals does not count)
                    (timeSigIndex == syncTrack.TimeSignatures.Count || syncTrack.Tempos[bpmIndex].Tick < syncTrack.TimeSignatures[timeSigIndex].Tick))
                {
                    var bpm = syncTrack.Tempos[bpmIndex++];
                    long microseconds = TempoChange.BpmToMicroSeconds(bpm.BeatsPerMinute);
                    timedEvents.Add((bpm.Tick, new SetTempoEvent(microseconds)));
                }
            }

            return FinalizeTrackChunk("TEMPO_TRACK", timedEvents);
        }

        private static TrackChunk GenerateEventsChunk(MoonSong sourceSong)
        {
            MidiEventList timedEvents = new();

            // Indexing the separate lists is the only way to
            // 1: Not allocate more space for a combined list, and
            // 2: Not rely on polymorphic queries
            int sectionIndex = 0;
            int eventIndex = 0;
            while (sectionIndex < sourceSong.sections.Count ||
                   eventIndex < sourceSong.events.Count)
            {
                // Generate in this order: sections, events
                while (sectionIndex < sourceSong.sections.Count &&
                    // Section comes before or at the same time as an event
                    (eventIndex == sourceSong.events.Count || sourceSong.sections[sectionIndex].tick <= sourceSong.events[eventIndex].tick))
                {
                    var section = sourceSong.sections[sectionIndex++];
                    timedEvents.Add((section.tick, new Melanchall.DryWetMidi.Core.TextEvent(section.text)));
                }

                while (eventIndex < sourceSong.events.Count &&
                    // Event comes before a section (equals does not count)
                    (sectionIndex == sourceSong.sections.Count || sourceSong.events[eventIndex].tick < sourceSong.sections[sectionIndex].tick))
                {
                    var ev = sourceSong.events[eventIndex++];
                    timedEvents.Add((ev.tick, new Melanchall.DryWetMidi.Core.TextEvent(ev.text)));
                }
            }

            return FinalizeTrackChunk(EVENTS_TRACK, timedEvents);
        }

        private static TrackChunk GenerateTrackChunk(MoonSong sourceSong, MoonInstrument instrument)
        {
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            var timedEvents = new MidiEventList();

            bool singleDifficulty = gameMode is GameMode.Vocals;

            // Text event flags to enable extended features
            if (gameMode == GameMode.Drums)
                timedEvents.Add((0, new Melanchall.DryWetMidi.Core.TextEvent($"[{CHART_DYNAMICS_TEXT}]")));
            else if (gameMode == GameMode.Guitar)
                timedEvents.Add((0, new Melanchall.DryWetMidi.Core.TextEvent($"[{ENHANCED_OPENS_TEXT}]")));

            long lastNoteTick = 0;
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                if (singleDifficulty && difficulty != Difficulty.Expert)
                    continue;

                var chart = sourceSong.GetChart(instrument, difficulty);

                // Indexing the separate lists is the only way to
                // 1: Not allocate more space for a combined list, and
                // 2: Not rely on polymorphic queries
                int noteIndex = 0;
                int phraseIndex = difficulty == Difficulty.Expert? 0 : chart.specialPhrases.Count;
                int eventIndex = difficulty == Difficulty.Expert ? 0 : chart.events.Count;

                while (noteIndex < chart.notes.Count ||
                        phraseIndex < chart.specialPhrases.Count ||
                        eventIndex < chart.events.Count)
                {
                    // Generate in this order: phrases, notes, then events
                    while (phraseIndex < chart.specialPhrases.Count &&
                        // Phrase comes before or at the same time as a note
                        (noteIndex == chart.notes.Count || chart.specialPhrases[phraseIndex].tick <= chart.notes[noteIndex].tick) &&
                        // Phrase comes before or at the same time as an event
                        (eventIndex == chart.events.Count || chart.specialPhrases[phraseIndex].tick <= chart.events[eventIndex].tick))
                        GenerateSpecialPhrase(timedEvents, chart.specialPhrases[phraseIndex++], gameMode);

                    while (noteIndex < chart.notes.Count &&
                        // Note comes before a phrase (equals does not count)
                        (phraseIndex == chart.specialPhrases.Count || chart.notes[noteIndex].tick < chart.specialPhrases[phraseIndex].tick) &&
                        // Note comes before or at the same time as an event
                        (eventIndex  == chart.events.Count         || chart.notes[noteIndex].tick <= chart.events[eventIndex].tick))
                        GenerateNote(timedEvents, chart.notes[noteIndex++], gameMode, difficulty, ref lastNoteTick);

                    while (eventIndex < chart.events.Count &&
                        // Event comes before a phrase (equals does not count)
                        (phraseIndex == chart.specialPhrases.Count || chart.events[eventIndex].tick < chart.specialPhrases[phraseIndex].tick) &&
                        // Event comes before a note (equals does not count)
                        (noteIndex   == chart.notes.Count          || chart.events[eventIndex].tick < chart.notes[noteIndex].tick))
                    {
                        var ev = chart.events[eventIndex++];
                        timedEvents.Add((ev.tick, new Melanchall.DryWetMidi.Core.TextEvent(ev.text)));
                    }
                }
            }

            // Write events to new track
            string instrumentName = InstrumentToNameLookup[instrument];
            return FinalizeTrackChunk(instrumentName, timedEvents);
        }

        private static void GenerateNote(MidiEventList events, MoonNote note, GameMode gameMode, Difficulty difficulty,
            ref long lastNoteTick)
        {
            // Apply sustain cutoffs
            if (note.length < (SUSTAIN_CUTOFF_THRESHOLD) && gameMode != GameMode.Vocals)
                note.length = 0;

            // Write notes
            long startTick = note.tick;
            long endTick = startTick + Math.Max(note.length, 1);
            long lastNoteDelta = startTick - lastNoteTick;
            GenerateNotesForDifficulty<NoteOnEvent>(events, gameMode, difficulty, note, startTick, VELOCITY, lastNoteDelta);
            GenerateNotesForDifficulty<NoteOffEvent>(events, gameMode, difficulty, note, endTick, 0, lastNoteDelta);

            // Keep track of last note tick for HOPO marking
            lastNoteTick = startTick;
        }

        private static void GenerateNotesForDifficulty<TNoteEvent>(MidiEventList events, GameMode gameMode, Difficulty difficulty,
            MoonNote note, long noteTick, byte velocity, long lastStartDelta)
            where TNoteEvent : NoteEvent, new()
        {
            // This code is somewhat hacky and makes a lot of assumptions, but it does the job

            // Whether or not certain note flags can be placed
            // 5/6-fret guitar
            bool canForceStrum = gameMode is not GameMode.Drums or GameMode.ProGuitar;
            bool canForceHopo = gameMode is not GameMode.Drums;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar && difficulty == Difficulty.Expert; // Tap marker is all-difficulty
            // Drums
            bool canTom = gameMode is GameMode.Drums && difficulty == Difficulty.Expert; // Tom markers are all-difficulty
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            // Note start + offsets
            int difficultyStart = InstrumentDifficultyStartLookup[gameMode][difficulty];
            var noteOffsetLookup = InstrumentNoteOffsetLookup[gameMode];
            var forceOffsetLookup = InstrumentForceOffsetLookup[gameMode];
            var channelFlagLookup = InstrumentChannelFlagLookup[gameMode];

            // Note properties
            var flags = note.flags;
            int rawNote = gameMode switch {
                GameMode.Guitar => (int)note.guitarFret,
                GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                GameMode.ProGuitar => (int)note.proGuitarString,
                GameMode.Drums => (int)note.drumPad,
                GameMode.Vocals => note.vocalsPitch,
                _ => note.rawNote
            };

            // Note number
            byte noteNumber = (byte)(difficultyStart + noteOffsetLookup[rawNote]);
            if (canDoubleKick && rawNote == (int)DrumPad.Kick && (flags & Flags.DoubleKick) != 0)
                noteNumber--;

            // Drum dynamics
            if (canDynamics && velocity > 0)
            {
                if ((flags & Flags.ProDrums_Accent) != 0)
                    velocity = VELOCITY_ACCENT;
                else if ((flags & Flags.ProDrums_Ghost) != 0)
                    velocity = VELOCITY_GHOST;
            }

            // Pro Guitar fret number
            if (gameMode is GameMode.ProGuitar && velocity > 0)
                velocity = (byte)(100 + note.proGuitarFret);

            // Pro Guitar channel flags
            if (!channelFlagLookup.TryGetValue(flags, out byte channel))
                channel = 0;

            // Vocals percussion note
            if (gameMode is GameMode.Vocals && (flags & Flags.Vocals_Percussion) != 0)
                noteNumber = PERCUSSION_NOTE;

            // Main note
            var midiNote = new TNoteEvent()
            {
                NoteNumber = S(noteNumber),
                Velocity = S(velocity),
                DeltaTime = noteTick,
                Channel = F(channel)
            };
            events.Add((noteTick, midiNote));

            // Note flags
            if ((canForceStrum || canForceHopo) && (flags & Flags.Forced) != 0)
            {
                MoonNoteType type;
                if (canForceHopo && lastStartDelta >= HOPO_THRESHOLD)
                {
                    type = MoonNoteType.Hopo;
                    // Apply additional flag to match the parsed data
                    note.flags |= Flags.Forced_Hopo;
                }
                else
                {
                    type = MoonNoteType.Strum;
                    // Apply additional flag to match the parsed data
                    note.flags |= Flags.Forced_Strum;
                }

                byte forceNote = (byte)(difficultyStart + forceOffsetLookup[type]);
                midiNote = new TNoteEvent() { NoteNumber = S(forceNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTap && (flags & Flags.Tap) != 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S(TAP_NOTE_CH), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTom && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)rawNote, out int padNote) &&
                (flags & Flags.ProDrums_Cymbal) == 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S((byte)padNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
        }

        private static void GenerateSpecialPhrase(MidiEventList events, MoonPhrase phrase, GameMode gameMode)
        {
            // Get note number (ignore if not supported by the game mode)
            if (!InstrumentSpecialPhraseLookup[gameMode].TryGetValue(phrase.type, out byte[]? notesToAdd))
                return;

            // Write notes
            long startTick = phrase.tick;
            long endTick = startTick + Math.Max(phrase.length, 1);
            foreach (byte note in notesToAdd)
            {
                events.Add((startTick, new NoteOnEvent() { NoteNumber = S(note), Velocity = S(VELOCITY) }));
                events.Add((endTick, new NoteOffEvent() { NoteNumber = S(note), Velocity = S(0) }));
            }
        }

        private static TrackChunk FinalizeTrackChunk(string trackName, MidiEventList events)
        {
            // Sort events by time
            events.Sort((ev1, ev2) => {
                if (ev1.absoluteTick > ev2.absoluteTick)
                    return 1;
                else if (ev1.absoluteTick < ev2.absoluteTick)
                    return -1;

                // Determine priority for certain types of events
                return (ev1.midiEvent, ev2.midiEvent) switch {
                    // Same-type note events should be sorted by note number
                    // Not *entirely* necessary, but without this then
                    // sorting is inconsistent and will throw an exception
                    (NoteOnEvent on1, NoteOnEvent on2) =>
                        on1.NoteNumber > on2.NoteNumber ? 1
                        : on1.NoteNumber < on2.NoteNumber ? -1
                        : 0,
                    (NoteOffEvent off1, NoteOffEvent off2) =>
                        off1.NoteNumber > off2.NoteNumber ? 1
                        : off1.NoteNumber < off2.NoteNumber ? -1
                        : 0,
                    // Note on events should come last, and note offs first
                    (NoteOnEvent, _) => 1,
                    (NoteOffEvent, _) => -1,
                    // The ordering of other events doesn't matter
                    _ => 0
                };
            });

            // Calculate delta time
            long previousTick = 0;
            foreach (var (tick, midi) in events)
            {
                long delta = tick - previousTick;
                midi.DeltaTime = delta;
                previousTick = tick;
            }

            // Write events to new track
            // Track name is written here to ensure it is the first event
            var chunk = new TrackChunk(new SequenceTrackNameEvent(trackName));
            chunk.Events.AddRange(events.Select((ev) => ev.midiEvent));
            return chunk;
        }

        private static MidiFile GenerateMidi(MoonSong sourceSong)
        {
            var midi = new MidiFile(
                GenerateSyncChunk(sourceSong),
                GenerateEventsChunk(sourceSong)
            )
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision((short)sourceSong.resolution)
            };

            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                var chunk = GenerateTrackChunk(sourceSong, instrument);
                midi.Chunks.Add(chunk);
            }

            return midi;
        }

        [TestCase]
        public void GenerateAndParseMidiFile()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());

            var sourceSong = GenerateSong();
            var midi = GenerateMidi(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = MidReader.ReadMidi(midi);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            VerifySong(sourceSong, parsedSong, InstrumentNoteOffsetLookup.Keys);
        }
    }
}