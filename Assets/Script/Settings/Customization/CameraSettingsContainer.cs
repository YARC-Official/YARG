using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class CameraSettingsContainer : CustomContent<CameraPreset>
    {
        public override IReadOnlyList<CameraPreset> DefaultPresets => CameraPreset.Defaults;

        public override string PresetTypeStringName => "CameraPreset";

        public CameraSettingsContainer(string contentDirectory) : base(contentDirectory)
        {
        }
    }
}