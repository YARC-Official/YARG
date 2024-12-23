using System;
using System.IO;
using BenchmarkDotNet.Running;

namespace YARG.Core.Benchmarks
{
    public static class Program
    {
        public const string CHART_PATH_VAR = "TEST_CHART_PATH";

        public static void Main()
        {
            ConsoleUtilities.WriteMenuHeader("YARG.Core Benchmarks", false);

            int choice = ConsoleUtilities.PromptChoice("Select a benchmark: ",
                "Chart Parsing",
                "Playground",
                "Exit"
            );

            switch (choice)
            {
                case 0: ChartParsingBenchmark(); break;
                case 1: BenchmarkPlayground(); break;
                case 2: return;
            }
        }

        private static void ChartParsingBenchmark()
        {
            ConsoleUtilities.WriteMenuHeader("Chart Parsing Benchmark");

            string chartPath = ConsoleUtilities.PromptTextInput("Please enter a chart file path: ", (input) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return "Invalid input!";

                if (!File.Exists(input))
                    return "File doesn't exist!";

                // TODO: CON file detection, whenever that's supported by YARG.Core
                if (Path.GetExtension(input) is not (".chart" or ".mid"))
                    return "Unsupported file type!";

                return null;
            });

            Environment.SetEnvironmentVariable(CHART_PATH_VAR, chartPath);
            Console.WriteLine();

            // A little unnecessary to split the file types into different tests, I suppose,
            // but why determine chart type repeatedly in the benchmark when you could do it once instead?
            string extension = Path.GetExtension(chartPath);
            switch (extension)
            {
                case ".chart":
                    BenchmarkRunner.Run<DotChartParsingBenchmarks>();
                    break;
                case ".mid":
                    BenchmarkRunner.Run<MidiParsingBenchmarks>();
                    break;
            }

            ConsoleUtilities.WaitForKey("Press any key to exit...");
        }

        private static void BenchmarkPlayground()
        {
            BenchmarkRunner.Run<BenchmarkPlayground>();
            ConsoleUtilities.WaitForKey("Press any key to exit...");
        }
    }
}