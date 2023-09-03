using System.IO;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public class CameraSettingsContainer : CustomContent<CameraPreset>
    {
        public CameraSettingsContainer(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void LoadFiles()
        {
            Content.Clear();

            PathHelper.SafeEnumerateFiles("*.json", true, (path) =>
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
    }
}