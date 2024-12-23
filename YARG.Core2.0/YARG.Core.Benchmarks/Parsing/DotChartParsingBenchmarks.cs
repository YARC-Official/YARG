using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using YARG.Core.Chart;

namespace YARG.Core.Benchmarks
{
    // [SimpleJob(RunStrategy.ColdStart, targetCount: 25, invocationCount: 1)]
    public class DotChartParsingBenchmarks
    {
        private string chartText;

        [GlobalSetup]
        public void Initialize()
        {
            string chartPath = Environment.GetEnvironmentVariable(Program.CHART_PATH_VAR);
            if (chartPath == null)
                throw new Exception("Could not find chart path environment variable!");

            chartText = File.ReadAllText(chartPath);
        }

        [Benchmark]
        public SongChart ChartParsing()
        {
            return SongChart.FromDotChart(in ParseSettings.Default_Chart, chartText);
        }
    }
}
