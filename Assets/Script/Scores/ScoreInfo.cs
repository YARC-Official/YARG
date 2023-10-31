using YARG.Core;

namespace YARG.Scores
{
    // TODO: Move to YARG.Core

    public enum StarAmount
    {
        None = 0,

        Star1,
        Star2,
        Star3,
        Star4,
        Star5,

        StarGold,
        StarSilver,
        StarBrutal
    }

    public struct ScoreInfo
    {
        public string PlayerName;

        public Instrument Instrument;
        public Difficulty Difficulty;

        public int Score;
        public StarAmount Stars;

        public float Percent;
        public bool IsFc;
    }
}