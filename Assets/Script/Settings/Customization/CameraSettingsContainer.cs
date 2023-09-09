using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public class CameraSettingsContainer : CustomContent<CameraPreset>
    {
        public override IEnumerable<CameraPreset> DefaultPresets => CameraPreset.Defaults;
        public override IEnumerable<string> DefaultPresetNames => DefaultPresets.Select(i => i.Name);

        public CameraSettingsContainer(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void LoadFiles()
        {
            Content.Clear();

            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var camera = JsonConvert.DeserializeObject<CameraPreset>(File.ReadAllText(path));

                Content.Add(camera.Name, camera);

                return true;
            });
        }

        public override void SaveItem(CameraPreset item)
        {
            throw new System.NotImplementedException();
        }

        public override void SetSettingsFromPreset(CameraPreset preset)
        {
            var s = SettingsManager.Settings;
            s.CameraPreset_FieldOfView.Data = preset.FieldOfView;
            s.CameraPreset_PositionY.Data   = preset.PositionY;
            s.CameraPreset_PositionZ.Data   = preset.PositionZ;
            s.CameraPreset_Rotation.Data    = preset.Rotation;
            s.CameraPreset_FadeStart.Data   = preset.FadeStart;
            s.CameraPreset_FadeLength.Data  = preset.FadeLength;
            s.CameraPreset_CurveFactor.Data = preset.CurveFactor;
        }

        public override void SetPresetFromSettings(CameraPreset preset)
        {
            var s = SettingsManager.Settings;
            preset.FieldOfView = s.CameraPreset_FieldOfView.Data;
            preset.PositionY   = s.CameraPreset_PositionY.Data;
            preset.PositionZ   = s.CameraPreset_PositionZ.Data;
            preset.Rotation    = s.CameraPreset_Rotation.Data;
            preset.FadeStart   = s.CameraPreset_FadeStart.Data;
            preset.FadeLength  = s.CameraPreset_FadeLength.Data;
            preset.CurveFactor = s.CameraPreset_CurveFactor.Data;
        }
    }
}