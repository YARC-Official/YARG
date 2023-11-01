using YARG.Core;

namespace YARG.Scores
{
    // TODO: Move to YARG.Core

    public enum StarAmount
    {
        None = 0,

        Star1 = 1,
        Star2 = 2,
        Star3 = 3,
        Star4 = 4,
        Star5 = 5,

        StarGold,
        StarSilver,
        StarBrutal
    }

    public static class StarAmountHelper {
        public static StarAmount GetStarsFromInt(int stars)
        {
            // TODO: Deal with brutal and silver stars

            if (stars is >= 0 and <= 5)
            {
                return (StarAmount) stars;
            }

            if (stars == 6)
            {
                return StarAmount.StarGold;
            }

            return StarAmount.None;
        }
    }

    public struct ScoreInfo
    {
        public string PlayerName { get; set; }

        public Instrument Instrument { get; set; }
        public Difficulty Difficulty { get; set; }

        public int Score { get; set; }
        public StarAmount Stars { get; set; }

        public float Percent { get; set; }
        public bool IsFc { get; set; }
    }
}