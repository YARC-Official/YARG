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
        /// Aligned 1:1 with <see cref="Stats"/>'s offset samples: true for the "selected" side of
        /// this instrument's calibration filter (a strummed note for guitar, a kick for drums),
        /// false for the other side. Null if the instrument has no such distinction.
        /// </summary>
        public IReadOnlyList<bool> OffsetSampleFilterCategory;
    }

    public struct ScoreScreenStats
    {
        public PlayerScoreCard[] PlayerScores;

        public int BandStars;
        public int BandScore;

        public double MeanAverageOffset;

        /// <summary>
        /// Same as <see cref="MeanAverageOffset"/>, but for each player using only their filter
        /// category's samples (strums for guitar, kicks for drums; a player's full sample set if
        /// their instrument has no such distinction) before averaging across players. Null when no
        /// samples were recorded by any eligible player.
        /// </summary>
        public double? MeanAverageOffsetFilterCategoryOnly;

#nullable enable
        public ReplayInfo? ReplayInfo;
        public bool? ReplayWasConsistent;
#nullable disable
    }
}