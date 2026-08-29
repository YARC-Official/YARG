using System.Collections.Generic;
using YARG.Core.Engine;
using YARG.Core.Replays;
using YARG.Player;
using YARG.Replays;

namespace YARG.Menu.ScoreScreen
{
    public struct PlayerScoreCard
    {
        public bool  IsHighScore;
        public bool  IsReplay;

        public YargPlayer Player;
        public BaseStats  Stats;

        /// <summary>
        /// Aligned 1:1 with <see cref="Stats"/>'s offset samples: true for a strummed note, false
        /// for a HOPO/tap. Null if the instrument has no such distinction.
        /// </summary>
        public IReadOnlyList<bool> OffsetSampleIsStrum;
    }

    public struct ScoreScreenStats
    {
        public PlayerScoreCard[] PlayerScores;

        public int BandStars;
        public int BandScore;

        public double MeanAverageOffset;

        /// <summary>
        /// Same as <see cref="MeanAverageOffset"/>, but averaging only strummed notes (excluding
        /// HOPOs/taps) per player before averaging across players. Null when no strummed notes
        /// were recorded by any eligible player.
        /// </summary>
        public double? MeanAverageOffsetStrumOnly;

#nullable enable
        public ReplayInfo? ReplayInfo;
        public bool? ReplayWasConsistent;
#nullable disable
    }
}