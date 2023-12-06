using System.Collections.Generic;

namespace YARG.Settings.Customization
{
    public partial class EnginePreset
    {
        public static EnginePreset Default = new("Default", true);

        public static readonly List<EnginePreset> Defaults = new()
        {
            Default
        };
    }
}