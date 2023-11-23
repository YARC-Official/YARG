using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class ColorProfileContainer : CustomContent<ColorProfile>
    {
        private static readonly List<ColorProfile> _defaultProfiles = new() { ColorProfile.Default };
        public override IReadOnlyList<ColorProfile> DefaultPresets => _defaultProfiles;

        public override string PresetTypeStringName => "ColorProfile";

        public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
        {
        }
    }
}