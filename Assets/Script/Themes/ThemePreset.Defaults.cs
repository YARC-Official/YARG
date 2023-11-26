using System.Collections.Generic;
using YARG.Core;

namespace YARG.Themes
{
    public partial class ThemePreset
    {
        public static ThemePreset Default = new ThemePreset("Rectangular", true)
        {
            AssetBundleThemePath = "Themes/Rectangular",
            SupportedGameModes =
            {
                GameMode.FiveFretGuitar,
                GameMode.SixFretGuitar,
                GameMode.FourLaneDrums,
                GameMode.FiveLaneDrums,
                GameMode.ProKeys
            }
        };

        public static readonly List<ThemePreset> Defaults = new()
        {
            Default,
            new ThemePreset("Circular (Beta)", true)
            {
                AssetBundleThemePath = "Themes/Circular",
                SupportedGameModes =
                {
                    GameMode.FiveFretGuitar
                }
            }
        };
    }
}