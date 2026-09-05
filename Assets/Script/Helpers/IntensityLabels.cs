using YARG.Localization;

namespace YARG.Helpers
{
    /// <summary>
    /// Shared intensity-tier naming, used by the filters and difficulty select menus.
    /// </summary>
    public static class IntensityLabels
    {
        private static readonly string[] LabelKeys =
        {
            "Menu.Filters.Intensities.WarmUp",
            "Menu.Filters.Intensities.Apprentice",
            "Menu.Filters.Intensities.Solid",
            "Menu.Filters.Intensities.Moderate",
            "Menu.Filters.Intensities.Challenging",
            "Menu.Filters.Intensities.Nightmare",
            "Menu.Filters.Intensities.Impossible",
        };

        public static int LabelCount => LabelKeys.Length;

        public const string UnknownKey = "Menu.Filters.Intensities.Unknown";
        public const string NoPartKey = "Menu.Filters.Intensities.NoPart";

        public static string GetLabelByIndex(int index)
        {
            if (index < 0) return null;
            if (index >= LabelKeys.Length) index = LabelKeys.Length - 1;

            return Localize.Key(LabelKeys[index]);
        }
    }
}
