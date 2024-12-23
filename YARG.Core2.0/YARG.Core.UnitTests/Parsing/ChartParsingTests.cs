using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.UnitTests.Parsing
{
    public class ChartParsingTests
    {
        private string? chartsDirectory;

        [SetUp]
        public void Setup()
        {
            // This will get the current WORKING directory (i.e. \bin\Debug)
            string workingDirectory = Environment.CurrentDirectory;

            // This will get the current PROJECT directory
            string projectDirectory = Directory.GetParent(workingDirectory)!.Parent!.Parent!.FullName;

            chartsDirectory = Path.Combine(projectDirectory, "Parsing", "Test Charts");
        }

        [TestCase("test.chart")]
        public void ParseChartFile(string notesFile)
        {
            YargLogger.AddLogListener(new DebugYargLogListener());

            Assert.DoesNotThrow(() =>
            {
                string chartPath = Path.Combine(chartsDirectory!, notesFile);
                var song = ChartReader.ReadFromFile(chartPath);
            });
        }

        [TestCase("test.mid")]
        public void ParseMidiFile(string notesFile)
        {
            YargLogger.AddLogListener(new DebugYargLogListener());

            Assert.DoesNotThrow(() =>
            {
                string chartPath = Path.Combine(chartsDirectory!, notesFile);
                var song = MidReader.ReadMidi(chartPath);
            });
        }
    }
}