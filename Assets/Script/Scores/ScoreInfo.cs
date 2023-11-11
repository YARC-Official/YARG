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
            return stars switch
            {
                >= 0 and <= 5 => (StarAmount) stars,
                6             => StarAmount.StarGold,
                _             => StarAmount.None
            };
        }
    }
}