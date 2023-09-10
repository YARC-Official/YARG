using System.IO;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public static class CustomContentManager
    {
        private const string CUSTOM_DIRECTORY = "custom";
        private static string CustomizationDirectory => Path.Combine(PathHelper.PersistentDataPath, CUSTOM_DIRECTORY);

        // public static readonly ColorProfileContainer   ColorProfiles;
        public static readonly CameraSettingsContainer CameraSettings;

        static CustomContentManager()
        {
            // ColorProfiles = new ColorProfileContainer(Path.Combine(CustomizationDirectory, "colors"));
            CameraSettings = new CameraSettingsContainer(Path.Combine(CustomizationDirectory, "cameras"));
        }

        public static void LoadAll()
        {
            // ColorProfiles.LoadFiles();
            CameraSettings.LoadFiles();
        }

        public static void SaveAll()
        {
            // ColorProfiles.SaveAll();
            CameraSettings.SaveAll();
        }
    }
}