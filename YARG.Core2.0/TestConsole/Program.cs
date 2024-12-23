using System;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.TestConsole;

#pragma warning disable CS8321 // Local function is declared but never used

// Simple console app for quick and dirty testing
// Changes to this generally shouldn't be committed, but common test procedures are fine to keep around

YargLogger.MinimumLogLevel = LogLevel.Debug;
YargLogger.AddLogListener(new DebugYargLogListener());

Console.WriteLine();
Console.WriteLine("Press any key to continue...");
YargLogger.KillLogger();
Console.ReadKey(intercept: true);

static SongChart LoadIniChart(string directory)
{
    return CacheLoader.LoadIni(CacheLoader.LoadCache(), directory)
        .LoadChart() ?? throw new Exception("Could not load chart!");
}

static SongChart LoadSngChart(string filePath)
{
    return CacheLoader.LoadSng(CacheLoader.LoadCache(), filePath)
        .LoadChart() ?? throw new Exception("Could not load chart!");
}

static SongChart LoadCONChart(string songId)
{
    return CacheLoader.LoadCON(CacheLoader.LoadCache(), songId)
        .LoadChart() ?? throw new Exception("Could not load chart!");
}
