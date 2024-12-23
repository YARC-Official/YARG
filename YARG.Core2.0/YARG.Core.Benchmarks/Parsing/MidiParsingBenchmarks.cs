using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Chart;

namespace YARG.Core.Benchmarks
{
    // [SimpleJob(RunStrategy.ColdStart, targetCount: 25, invocationCount: 1)]
    public class MidiParsingBenchmarks
    {
        private MidiFile midi;

        [GlobalSetup]
        public void Initialize()
        {
            string chartPath = Environment.GetEnvironmentVariable(Program.CHART_PATH_VAR);
            if (chartPath == null)
                throw new Exception("Could not find chart path environment variable!");

            midi = MidiFile.Read(chartPath);
        }

        [Benchmark]
        public SongChart ChartParsing()
        {
            return SongChart.FromMidi(in ParseSettings.Default_Midi, midi);
        }
    }
}
