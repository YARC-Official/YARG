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
        public float AverageMultiplier;

        public YargPlayer Player;
        public BaseStats  Stats;

        // Per-phrase normalized hit percents for vocals players, in song order. Null for
        // non-vocals players. Feeds the vocals phrase summary in the Advanced view.
        public IReadOnlyList<float> VocalPhrasePercents;

        // Vocal percussion hits / total (0 for non-vocals or charts without percussion).
        public int VocalPercussionHits;
        public int VocalPercussionTotal;
    }

    public struct ScoreScreenStats
    {
        public PlayerScoreCard[] PlayerScores;

        public int BandStars;
        public int BandScore;

#nullable enable
        public ReplayInfo? ReplayInfo;
#nullable disable
    }
}