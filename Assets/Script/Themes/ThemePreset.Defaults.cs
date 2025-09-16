using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Game;
using static YARG.Themes.ThemeManager;

namespace YARG.Themes
{
    public partial class ThemePreset
    {
        public static ThemePreset Default = new("Rectangular", true)
        {
            AssetBundleThemePath = "Themes/Rectangular",
            SupportedStyles =
            {
                VisualStyle.FiveFretGuitar,
                VisualStyle.SixFretGuitar,
                VisualStyle.FourLaneDrums,
                VisualStyle.FiveLaneDrums,
                VisualStyle.FiveLaneKeys,
                VisualStyle.ProKeys
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
                SupportedStyles =
                {
                    VisualStyle.FiveFretGuitar,
                    VisualStyle.FiveLaneKeys
                },
                PreferredColorProfile = ColorProfile.CircularDefault.Id,
                PreferredCameraPreset = CameraPreset.CircularDefault.Id,
            },
            new ThemePreset("YARG on Fire", true)
            {
                AssetBundleThemePath = "Themes/AprilFools",
                SupportedStyles =
                {
                    VisualStyle.FiveFretGuitar,
                    VisualStyle.FourLaneDrums,
                    VisualStyle.FiveLaneDrums,
                    VisualStyle.FiveLaneKeys
                },
                PreferredColorProfile = ColorProfile.AprilFoolsDefault.Id,
                PreferredCameraPreset = CameraPreset.CircularDefault.Id,
            }
        };
    }
}