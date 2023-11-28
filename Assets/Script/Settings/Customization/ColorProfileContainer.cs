using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class ColorProfileContainer : CustomContent<ColorProfile>
    {
        public override IReadOnlyList<ColorProfile> DefaultPresets => ColorProfile.Defaults;

        public override string PresetTypeStringName => "ColorProfile";

        public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
        {
        }
    }
}