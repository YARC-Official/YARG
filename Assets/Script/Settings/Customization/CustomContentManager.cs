using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public static partial class CustomContentManager
    {
        private const string CUSTOMIZATION_DIRECTORY = "customization";
        private static string CustomizationDirectory => Path.Combine(PathHelper.PersistentDataPath, CUSTOMIZATION_DIRECTORY);

        public static void LoadContent()
        {
            ColorProfiles.Load();
            CameraSettings.Load();
        }

        private static void LoadFiles<T>(Dictionary<string, T> list, string folderPath)
        {
            list.Clear();

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                return;
            }

            PathHelper.SafeEnumerateFiles(folderPath, "*.json", (path) =>
            {
                string jsonFile = File.ReadAllText(path);
                var item = JsonConvert.DeserializeObject<T>(jsonFile);
                if (item is null)
                    return true;

                string fileName = Path.GetFileNameWithoutExtension(path);
                list.Add(fileName, item);
                return true;
            });
        }

        private static void SaveItem<T>(T item, string fileName, string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            FileExplorerHelper.OpenSaveFile(folderPath, fileName, "json", (path) =>
            {
                string profilesJson = JsonConvert.SerializeObject(item, Formatting.Indented);
                File.WriteAllText(path, profilesJson);
            });
        }
    }
}