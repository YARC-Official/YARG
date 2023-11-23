using System.Collections.Generic;

namespace YARG.Themes
{
    public partial class ThemePreset
    {
        public static ThemePreset Default = new ThemePreset("Rectangular", true)
        {
            AssetBundleThemePath = "Themes/Rectangular"
        };

        public static readonly List<ThemePreset> Defaults = new()
        {
            Default
        };
    }
}