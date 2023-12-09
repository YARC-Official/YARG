using System.Collections.Generic;

namespace YARG.Settings.Customization
{
    public partial class EnginePreset
    {
        public static EnginePreset Default = new("Default", true);

        public static EnginePreset Casual = new("Casual", true)
        {
            FiveFretGuitar =
            {
                AntiGhosting = false,
                InfiniteFrontEnd = true
            }
        };

        public static EnginePreset Precision = new("Precision", true);

        public static readonly List<EnginePreset> Defaults = new()
        {
            Default,
            Casual,
            Precision
        };
    }
}