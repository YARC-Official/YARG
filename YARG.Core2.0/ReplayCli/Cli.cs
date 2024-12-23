using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Replays;

namespace ReplayCli;

public partial class Cli
{
    private string _songPath;
    private string _replayPath;
    private AnalyzerMode _runMode;

    private ReplayInfo _replayInfo;
    private ReplayData _replayData;

    /// <summary>
    /// Parses the specified arguments.
    /// </summary>
    /// <returns>
    /// Returns <c>false</c> if there was an error, or the help menu was printed.
    /// </returns>
    public bool ParseArguments(string[] args)
    {
        if (args.Length < 2)
        {
            PrintHelpMessage();
            return false;
        }

        // Argument 1 is the mode

        _runMode = args[0] switch
        {
            "verify"       => AnalyzerMode.Verify,
            "simulate_fps" => AnalyzerMode.SimulateFps,
            "dump_inputs"  => AnalyzerMode.DumpInputs,
            "read"         => AnalyzerMode.Read,
            _              => AnalyzerMode.None
        };

        if (_runMode == AnalyzerMode.None)
        {
            PrintHelpMessage();
            return false;
        }

        // Argument 2 is the path to the replay
        _replayPath = args[1].Trim();
        if (!File.Exists(_replayPath))
        {
            Console.WriteLine("ERROR: Replay file does not exist!");
            return false;
        }

        // Rest of the arguments are options
        for (int i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--song":
                case "-s":
                {
                    i++;

                    _songPath = args[i].Trim();
                    if (!Directory.Exists(_songPath))
                    {
                        Console.WriteLine("ERROR: Song folder does not exist!");
                    }

                    break;
                }
                case "--help":
                case "-h":
                {
                    PrintHelpMessage();
                    return false;
                }
            }
        }

        return true;
    }

    private static void PrintHelpMessage()
    {
        Console.WriteLine(
            """
            Usage: ReplayCli [mode] [replay-path] [options...]

            Mode: the run mode of the analyzer
              verify         Verifies the replay's metadata.
              simulate_fps   Simulates FPS updates to verify the engines consistency.
              dump_inputs    Dumps the replay's inputs.

            Replay Path: the path to the replay

            Options:
              --song     | -s    Path to `song.ini` folder (required in `verify` and `simulate_fps` modes).
              --help     | -h    Show this help message.
            """);
    }

    private void PrintReplayMetadata()
    {
        Console.WriteLine($"Players ({_replayData.PlayerCount}):");
        for (int i = 0; i < _replayData.Frames.Length; i++)
        {
            var frame = _replayData.Frames[i];
            var profile = frame.Profile;

            Console.WriteLine($"{i}. {profile.Name}, {profile.CurrentInstrument} ({profile.CurrentDifficulty})");

            // Indent the engine parameters
            Console.WriteLine($"   {frame.EngineParameters.ToString()?.ReplaceLineEndings("\n   ")}");
        }

        Console.WriteLine($"Band score: {_replayInfo.BandScore} (as per metadata)\n");
    }

    /// <summary>
    /// Runs the analyzer using the arguments parsed in <see cref="ParseArguments"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the run succeeded, <c>false</c> if it didn't.
    /// </returns>
    public bool Run()
    {
        (var result, _replayInfo, _replayData) = ReplayIO.TryDeserialize(_replayPath);
        if (result != ReplayReadResult.Valid)
        {
            Console.WriteLine($"ERROR: Failed to load replay. Read Result: {result}.");
            return false;
        }

        return _runMode switch
        {
            AnalyzerMode.Verify      => RunVerify(),
            AnalyzerMode.SimulateFps => RunSimulateFps(),
            AnalyzerMode.DumpInputs  => RunDumpInputs(),
            AnalyzerMode.Read        => RunRead(),
            _                        => false
        };
    }

    private SongChart ReadChart()
    {
        string songIni = Path.Combine(_songPath, "song.ini");
        string notesMid = Path.Combine(_songPath, "notes.mid");
        string notesChart = Path.Combine(_songPath, "notes.chart");
        if (!File.Exists(songIni) || (!File.Exists(notesMid) && !File.Exists(notesChart)))
        {
            Console.WriteLine(
                "ERROR: Song directory does not contain necessary song files (song.ini, notes.mid/chart).");
            return null;
        }

        SongChart chart;
        try
        {
            // TODO: Prevent this workaround from being needed
            var parseSettings = ParseSettings.Default;
            parseSettings.DrumsType = DrumsType.FourLane;

            if (File.Exists(notesMid))
            {
                chart = SongChart.FromFile(parseSettings, notesMid);
            }
            else
            {
                chart = SongChart.FromFile(parseSettings, notesChart);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Failed to load notes file. \n{e}");
            return null;
        }

        return chart;
    }

    private static void PrintStatDifferences(BaseStats originalStats, BaseStats resultStats)
    {
        static void PrintStatDifference<T>(string name, T frameStat, T resultStat)
        where T : IEquatable<T>
        {
            if (frameStat.Equals(resultStat))
                Console.WriteLine($"- {name + ":",-31} {frameStat,-12} (identical)");
            else
                Console.WriteLine($"- {name + ":",-31} {frameStat,-10} -> {resultStat}");
        }

        Console.WriteLine($"Base stats:");
        PrintStatDifference("CommittedScore",         originalStats.CommittedScore,         resultStats.CommittedScore);
        PrintStatDifference("PendingScore",           originalStats.PendingScore,           resultStats.PendingScore);
        PrintStatDifference("TotalScore",             originalStats.TotalScore,             resultStats.TotalScore);
        PrintStatDifference("StarScore",              originalStats.StarScore,              resultStats.StarScore);
        PrintStatDifference("Combo",                  originalStats.Combo,                  resultStats.Combo);
        PrintStatDifference("MaxCombo",               originalStats.MaxCombo,               resultStats.MaxCombo);
        PrintStatDifference("ScoreMultiplier",        originalStats.ScoreMultiplier,        resultStats.ScoreMultiplier);
        PrintStatDifference("NotesHit",               originalStats.NotesHit,               resultStats.NotesHit);
        PrintStatDifference("TotalNotes",             originalStats.TotalNotes,             resultStats.TotalNotes);
        PrintStatDifference("NotesMissed",            originalStats.NotesMissed,            resultStats.NotesMissed);
        PrintStatDifference("Percent",                originalStats.Percent,                resultStats.Percent);
        PrintStatDifference("StarPowerTickAmount",    originalStats.StarPowerTickAmount,    resultStats.StarPowerTickAmount);
        PrintStatDifference("TotalStarPowerTicks",    originalStats.TotalStarPowerTicks,    resultStats.TotalStarPowerTicks);
        PrintStatDifference("TimeInStarPower",        originalStats.TimeInStarPower,        resultStats.TimeInStarPower);
        PrintStatDifference("IsStarPowerActive",      originalStats.IsStarPowerActive,      resultStats.IsStarPowerActive);
        PrintStatDifference("StarPowerPhrasesHit",    originalStats.StarPowerPhrasesHit,    resultStats.StarPowerPhrasesHit);
        PrintStatDifference("TotalStarPowerPhrases",  originalStats.TotalStarPowerPhrases,  resultStats.TotalStarPowerPhrases);
        PrintStatDifference("StarPowerPhrasesMissed", originalStats.StarPowerPhrasesMissed, resultStats.StarPowerPhrasesMissed);
        PrintStatDifference("SoloBonuses",            originalStats.SoloBonuses,            resultStats.SoloBonuses);
        PrintStatDifference("StarPowerScore",         originalStats.StarPowerScore,         resultStats.StarPowerScore);
        // PrintStatDifference("Stars",                  originalStats.Stars,                  resultStats.Stars);

        Console.WriteLine();
        switch (originalStats, resultStats)
        {
            case (GuitarStats originalGuitar, GuitarStats resultGuitar):
            {
                Console.WriteLine("Guitar stats:");
                PrintStatDifference("Overstrums",             originalGuitar.Overstrums,             resultGuitar.Overstrums);
                PrintStatDifference("HoposStrummed",          originalGuitar.HoposStrummed,          resultGuitar.HoposStrummed);
                PrintStatDifference("GhostInputs",            originalGuitar.GhostInputs,            resultGuitar.GhostInputs);
                PrintStatDifference("StarPowerWhammyTicks",   originalGuitar.StarPowerWhammyTicks,   resultGuitar.StarPowerWhammyTicks);
                PrintStatDifference("SustainScore",           originalGuitar.SustainScore,           resultGuitar.SustainScore);
                break;
            }
            case (DrumsStats originalDrums, DrumsStats resultDrums):
            {
                Console.WriteLine("Drums stats:");
                PrintStatDifference("Overhits",      originalDrums.Overhits,      resultDrums.Overhits);
                break;
            }
            case (VocalsStats originalVocals, VocalsStats resultVocals):
            {
                Console.WriteLine("Vocals stats:");
                PrintStatDifference("TicksHit",      originalVocals.TicksHit,      resultVocals.TicksHit);
                PrintStatDifference("TicksMissed",   originalVocals.TicksMissed,   resultVocals.TicksMissed);
                PrintStatDifference("TotalTicks",    originalVocals.TotalTicks,    resultVocals.TotalTicks);
                break;
            }
            case (ProKeysStats originalKeys, ProKeysStats resultKeys):
            {
                Console.WriteLine("Pro Keys stats:");
                PrintStatDifference("Overhits",      originalKeys.Overhits,      resultKeys.Overhits);
                break;
            }
            default:
            {
                if (originalStats.GetType() != resultStats.GetType())
                    Console.WriteLine($"Stats types do not match! Original: {originalStats.GetType()}, result: {resultStats.GetType()}");
                else
                    Console.WriteLine($"Unhandled stats type {originalStats.GetType()}!");
                break;
            }
        }
    }
}