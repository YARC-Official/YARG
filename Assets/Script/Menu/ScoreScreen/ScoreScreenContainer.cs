using YARG.Core.Engine;
using YARG.Player;

namespace YARG.Menu.ScoreScreen
{
    public struct PlayerScoreCard
    {
        public bool IsHighScore;

        public YargPlayer Player;
        public BaseStats  Stats;
    }

    public struct ScoreScreenStats
    {
        public PlayerScoreCard[] PlayerScores;

        public int BandStars;
        public int BandScore;
    }
}