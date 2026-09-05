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
    }

    public struct ScoreScreenStats
    {
        public PlayerScoreCard[] PlayerScores;

        public int BandStars;
        public int BandScore;

        public double MeanAverageOffset;

        /// <summary>
        /// The record that was standing before this run: the player's own best for a
        /// single human player, or the band best when several humans played.
        /// Null when there is no previous record (or no human players).
        /// </summary>
        public int? PreviousBest;

#nullable enable
        public ReplayInfo? ReplayInfo;
        public bool? ReplayWasConsistent;
#nullable disable
    }
}