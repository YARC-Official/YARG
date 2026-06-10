namespace YARG.Gameplay.Vocals
{
    // Ordered worst-to-best so the ordinal can be used directly for sorting tallies.
    public enum VocalPhraseGrade
    {
        Awful,
        Messy,
        Okay,
        Good,
        Strong,
        Awesome
    }

    public static class VocalPhraseGradeExtensions
    {
        // Inclusive lower bound per tier, indexed by (int) VocalPhraseGrade (worst -> best).
        private static readonly double[] LowerBounds = { 0.0, 0.1, 0.6, 0.7, 0.8, 1.0 };

        public static VocalPhraseGrade Classify(double normalizedPercent)
        {
            for (int i = LowerBounds.Length - 1; i >= 0; i--)
            {
                if (normalizedPercent >= LowerBounds[i])
                {
                    return (VocalPhraseGrade) i;
                }
            }

            return VocalPhraseGrade.Awful;
        }

        public static double LowerBound(this VocalPhraseGrade grade)
        {
            return LowerBounds[(int) grade];
        }

        public static string ToLocalizationKey(this VocalPhraseGrade grade)
        {
            return grade switch
            {
                VocalPhraseGrade.Awesome => "Awesome",
                VocalPhraseGrade.Strong  => "Strong",
                VocalPhraseGrade.Good    => "Good",
                VocalPhraseGrade.Okay    => "Okay",
                VocalPhraseGrade.Messy   => "Messy",
                _                        => "Awful"
            };
        }
    }
}
