using System.Collections.Generic;
using YARG.Core.Engine;

namespace YARG.Core.Replays.Analyzer
{
    public struct AnalysisResult
    {
        public bool Passed;

        public ReplayFrame Frame;

        public BaseStats OriginalStats;
        public BaseStats ResultStats;

        public int ScoreDifference;
    }
}