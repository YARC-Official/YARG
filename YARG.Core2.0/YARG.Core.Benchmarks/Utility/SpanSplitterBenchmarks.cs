using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using YARG.Core.UnitTests.Parsing;
using YARG.Core.Utility;

namespace YARG.Core.Benchmarks
{
    public class SpanSplitterBenchmarks
    {
        private string searchString;

        [GlobalSetup]
        public void Initialize()
        {
            searchString = ChartParseBehaviorTests.GenerateChartFile();
        }

        [Benchmark]
        [Arguments('\n')]
        [Arguments(' ')]
        public int Split(char searchChar)
        {
            int splitCount = 0;
            foreach (var _ in searchString.SplitAsSpan(searchChar))
                splitCount++;

            Debug.Assert(splitCount > 0);
            return splitCount;
        }

        [Benchmark]
        [Arguments('\n')]
        [Arguments(' ')]
        public int SplitTrimmed(char searchChar)
        {
            int splitCount = 0;
            foreach (var _ in searchString.SplitTrimmed(searchChar))
                splitCount++;

            Debug.Assert(splitCount > 0);
            return splitCount;
        }

        [Benchmark]
        [Arguments('\n')]
        [Arguments(' ')]
        public int SplitTrimmedAscii(char searchChar)
        {
            int splitCount = 0;
            foreach (var _ in searchString.SplitTrimmedAscii(searchChar))
                splitCount++;

            Debug.Assert(splitCount > 0);
            return splitCount;
        }

        [Benchmark]
        [Arguments('\n')]
        [Arguments(' ')]
        public int SplitTrimmedLatin1(char searchChar)
        {
            int splitCount = 0;
            foreach (var _ in searchString.SplitTrimmedLatin1(searchChar))
                splitCount++;

            Debug.Assert(splitCount > 0);
            return splitCount;
        }
    }
}
