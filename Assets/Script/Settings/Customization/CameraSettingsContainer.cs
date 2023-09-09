using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public class CameraSettingsContainer : CustomContent<CameraPreset>
    {
        public override IReadOnlyList<CameraPreset> DefaultPresets => CameraPreset.Defaults;

        public CameraSettingsContainer(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void LoadFiles()
        {
            Content.Clear();

            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var camera = JsonConvert.DeserializeObject<CameraPreset>(File.ReadAllText(path));

                Content.Add(camera);

                return true;
            });
        }

        public override void SaveItem(CameraPreset item)
        {
            throw new System.NotImplementedException();
        }

        public override void SetSettingsFromPreset(BasePreset preset)
        {
            if (preset is not CameraPreset p)
            {
                throw new InvalidOperationException("Invalid preset type!");
            }

            var s = SettingsManager.Settings;
            s.CameraPreset_FieldOfView.Data = p.FieldOfView;
            s.CameraPreset_PositionY.Data   = p.PositionY;
            s.CameraPreset_PositionZ.Data   = p.PositionZ;
            s.CameraPreset_Rotation.Data    = p.Rotation;
            s.CameraPreset_FadeStart.Data   = p.FadeStart;
            s.CameraPreset_FadeLength.Data  = p.FadeLength;
            s.CameraPreset_CurveFactor.Data = p.CurveFactor;
        }

        public override void SetPresetFromSettings(BasePreset preset)
        {
            if (preset is not CameraPreset p)
            {
                throw new InvalidOperationException("Invalid preset type!");
            }

            var s = SettingsManager.Settings;
            p.FieldOfView = s.CameraPreset_FieldOfView.Data;
            p.PositionY   = s.CameraPreset_PositionY.Data;
            p.PositionZ   = s.CameraPreset_PositionZ.Data;
            p.Rotation    = s.CameraPreset_Rotation.Data;
            p.FadeStart   = s.CameraPreset_FadeStart.Data;
            p.FadeLength  = s.CameraPreset_FadeLength.Data;
            p.CurveFactor = s.CameraPreset_CurveFactor.Data;
        }
    }
}