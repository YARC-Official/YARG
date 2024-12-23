using System.Text;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static ChartIOHelper;
    using static TextEvents;
    using static ParseBehaviorTests;

    public class ChartParseBehaviorTests
    {
        private static readonly Dictionary<MoonInstrument, string> InstrumentToNameLookup =
            InstrumentStrToEnumLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<Difficulty, string> DifficultyToNameLookup =
            TrackNameToTrackDifficultyLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<int, int> GuitarNoteLookup = new()
        {
            { (int)GuitarFret.Green,  0 },
            { (int)GuitarFret.Red,    1 },
            { (int)GuitarFret.Yellow, 2 },
            { (int)GuitarFret.Blue,   3 },
            { (int)GuitarFret.Orange, 4 },
            { (int)GuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, int> GhlGuitarNoteLookup = new()
        {
            { (int)GHLiveGuitarFret.Black1, 3 },
            { (int)GHLiveGuitarFret.Black2, 4 },
            { (int)GHLiveGuitarFret.Black3, 8 },
            { (int)GHLiveGuitarFret.White1, 0 },
            { (int)GHLiveGuitarFret.White2, 1 },
            { (int)GHLiveGuitarFret.White3, 2 },
            { (int)GHLiveGuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, int> DrumsNoteLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, int>> InstrumentToNoteLookupLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteLookup },
            { GameMode.Drums,     DrumsNoteLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteLookup },
        };

        private static readonly Dictionary<MoonPhrase.Type, int> SpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,           PHRASE_STARPOWER },
            { MoonPhrase.Type.Versus_Player1,      PHRASE_VERSUS_PLAYER_1 },
            { MoonPhrase.Type.Versus_Player2,      PHRASE_VERSUS_PLAYER_2 },
            { MoonPhrase.Type.TremoloLane,         PHRASE_TREMOLO_LANE },
            { MoonPhrase.Type.TrillLane,           PHRASE_TRILL_LANE },
            { MoonPhrase.Type.ProDrums_Activation, PHRASE_DRUM_FILL },
        };

        private static readonly List<MoonPhrase.Type> DrumsOnlySpecialPhrases = new()
        {
            MoonPhrase.Type.TremoloLane,
            MoonPhrase.Type.TrillLane,
            MoonPhrase.Type.ProDrums_Activation,
        };

        private const string NEWLINE = "\r\n";

        private static void GenerateSongSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_SONG}]{NEWLINE}{{{NEWLINE}");
            builder.Append($"  Resolution = {sourceSong.resolution}{NEWLINE}");
            builder.Append($"}}{NEWLINE}");
        }

        private static void GenerateSyncSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_SYNC_TRACK}]{NEWLINE}{{{NEWLINE}");

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
                    builder.Append($"  {ts.Tick} = TS {ts.Numerator} {(int) Math.Log2(ts.Denominator)}{NEWLINE}");
                }

                while (bpmIndex < syncTrack.Tempos.Count &&
                    // Bpm comes before a time sig (equals does not count)
                    (timeSigIndex == syncTrack.TimeSignatures.Count || syncTrack.Tempos[bpmIndex].Tick < syncTrack.TimeSignatures[timeSigIndex].Tick))
                {
                    var bpm = syncTrack.Tempos[bpmIndex++];
                    uint writtenBpm = (uint) (bpm.BeatsPerMinute * 1000);
                    builder.Append($"  {bpm.Tick} = B {writtenBpm}{NEWLINE}");
                }
            }
            builder.Append($"}}{NEWLINE}");
        }

        private static void GenerateEventsSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_EVENTS}]{NEWLINE}{{{NEWLINE}");

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
                    builder.Append($"  {section.tick} = E \"{section.text}\"");
                }

                while (eventIndex < sourceSong.events.Count &&
                    // Event comes before a section (equals does not count)
                    (sectionIndex == sourceSong.sections.Count || sourceSong.events[eventIndex].tick < sourceSong.sections[sectionIndex].tick))
                {
                    var ev = sourceSong.events[eventIndex++];
                    builder.Append($"  {ev.tick} = E \"{ev.text}\"");
                }
            }
            builder.Append($"}}{NEWLINE}");
        }

        private static void GenerateInstrumentSection(MoonSong sourceSong, StringBuilder builder, MoonInstrument instrument, Difficulty difficulty)
        {
            // Skip unsupported instruments
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            if (!InstrumentToNoteLookupLookup.ContainsKey(gameMode))
                return;

            var chart = sourceSong.GetChart(instrument, difficulty);

            string instrumentName = InstrumentToNameLookup[instrument];
            string difficultyName = DifficultyToNameLookup[difficulty];
            builder.Append($"[{difficultyName}{instrumentName}]{NEWLINE}{{{NEWLINE}");

            // Combine all of the chart events into a single list and sort them according to insertion order
            // Not very efficient, but adding a 4th list to the previous handling and
            // quadratically increasing the number of checks is just not sane lol
            List<MoonObject> combined = [..chart.notes, ..chart.specialPhrases, ..chart.events];
            combined.Sort((obj1, obj2) => obj1.InsertionCompareTo(obj2));

            List<MoonPhrase> phrasesToRemove = new();
            for (int i = 0; i < combined.Count; i++)
            {
                var chartObj = combined[i];

                switch (chartObj)
                {
                    case MoonNote note:
                        AppendNote(builder, note, gameMode);
                        break;
                    case MoonPhrase phrase:
                        // Drums-only phrases
                        if (gameMode is not GameMode.Drums && DrumsOnlySpecialPhrases.Contains(phrase.type))
                        {
                            phrasesToRemove.Add(phrase);
                            continue;
                        }

                        // Solos are written as text events in .chart
                        if (phrase.type is MoonPhrase.Type.Solo)
                        {
                            builder.Append($"  {phrase.tick} = E {SOLO_START}{NEWLINE}");
                            MoonObjectHelper.Insert(new MoonText(SOLO_END, phrase.tick + phrase.length), combined);
                            continue;
                        }

                        int phraseNumber = SpecialPhraseLookup[phrase.type];
                        builder.Append($"  {phrase.tick} = S {phraseNumber} {phrase.length}{NEWLINE}");
                        break;
                    case MoonText text:
                        builder.Append($"  {text.tick} = E {text.text}{NEWLINE}");
                        break;
                }
            }

            foreach (var phrase in phrasesToRemove)
            {
                chart.Remove(phrase);
            }

            builder.Append($"}}{NEWLINE}");
        }

        private static void AppendNote(StringBuilder builder, MoonNote note, GameMode gameMode)
        {
            uint tick = note.tick;
            var flags = note.flags;

            bool canForce = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canCymbal = gameMode is GameMode.Drums;
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            var noteLookup = InstrumentToNoteLookupLookup[gameMode];

            // Not technically necessary, but might as well lol
            int rawNote = gameMode switch {
                GameMode.Guitar => (int)note.guitarFret,
                GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                GameMode.ProGuitar => throw new NotSupportedException(".chart does not support Pro Guitar!"),
                GameMode.Drums => (int)note.drumPad,
                _ => note.rawNote
            };

            int chartNumber = noteLookup[rawNote];
            if (canDoubleKick && (flags & Flags.DoubleKick) != 0)
                chartNumber = NOTE_OFFSET_INSTRUMENT_PLUS;

            builder.Append($"  {tick} = N {chartNumber} {note.length}{NEWLINE}");
            if (canForce && (flags & Flags.Forced) != 0)
                builder.Append($"  {tick} = N 5 0{NEWLINE}");
            if (canTap && (flags & Flags.Tap) != 0)
                builder.Append($"  {tick} = N 6 0{NEWLINE}");
            if (canCymbal && (flags & Flags.ProDrums_Cymbal) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_PRO_DRUMS + chartNumber} 0{NEWLINE}");
            if (canDynamics && (flags & Flags.ProDrums_Accent) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_ACCENT + chartNumber} 0{NEWLINE}");
            if (canDynamics && (flags & Flags.ProDrums_Ghost) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_GHOST + chartNumber} 0{NEWLINE}");
        }

        private static string GenerateChartFile(MoonSong sourceSong)
        {
            var chartBuilder = new StringBuilder(5000);
            GenerateSongSection(sourceSong, chartBuilder);
            GenerateSyncSection(sourceSong, chartBuilder);
            GenerateEventsSection(sourceSong, chartBuilder);
            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                foreach (var difficulty in EnumExtensions<Difficulty>.Values)
                {
                    GenerateInstrumentSection(sourceSong, chartBuilder, instrument, difficulty);
                }
            }
            return chartBuilder.ToString();
        }

        public static string GenerateChartFile()
        {
            var song = GenerateSong();
            return GenerateChartFile(song);
        }

        [TestCase]
        public void GenerateAndParseChartFile()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());

            var sourceSong = GenerateSong();
            string chartText = GenerateChartFile(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = ChartReader.ReadFromText(chartText);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            VerifySong(sourceSong, parsedSong, InstrumentToNoteLookupLookup.Keys);
        }
    }
}