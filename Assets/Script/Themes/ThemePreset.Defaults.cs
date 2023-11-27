using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Themes
{
    public partial class ThemePreset
    {
        public static ThemePreset Default = new("Rectangular", true)
        {
            AssetBundleThemePath = "Themes/Rectangular",
            SupportedGameModes =
            {
                GameMode.FiveFretGuitar,
                GameMode.SixFretGuitar,
                GameMode.FourLaneDrums,
                GameMode.FiveLaneDrums,
                GameMode.ProKeys
            },
            PreferredColorProfile = ColorProfile.Default.Id,
            PreferredCameraPreset = CameraPreset.Default.Id
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
                },
                PreferredColorProfile = ColorProfile.CircularDefault.Id,
                PreferredCameraPreset = CameraPreset.CircularDefault.Id,
            }
        };
    }
}