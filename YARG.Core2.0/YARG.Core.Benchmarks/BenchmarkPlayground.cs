using BenchmarkDotNet.Attributes;

namespace YARG.Core.Benchmarks
{
    // For quick, investigative benchmarking that isn't meant to stick around
    public class BenchmarkPlayground
    {
        [GlobalSetup]
        public void Initialize()
        {
        }

        [Benchmark]
        // [Arguments(...)]
        public void Benchmark()
        {
        }
    }
}
