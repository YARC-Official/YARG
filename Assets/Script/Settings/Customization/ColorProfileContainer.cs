using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class ColorProfileContainer : CustomContent<ColorProfile>
    {
        protected override string ContentDirectory => "colors";

        public override string PresetTypeStringName => "ColorProfile";

        public override IReadOnlyList<ColorProfile> DefaultPresets => ColorProfile.Defaults;
    }
}