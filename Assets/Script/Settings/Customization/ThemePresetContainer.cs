using System.Collections.Generic;
using YARG.Themes;

namespace YARG.Settings.Customization
{
    public class ThemePresetContainer : CustomContent<ThemePreset>
    {
        protected override string ContentDirectory => "themes";

        public override string PresetTypeStringName => "ThemePreset";

        public override IReadOnlyList<ThemePreset> DefaultPresets => ThemePreset.Defaults;
    }
}