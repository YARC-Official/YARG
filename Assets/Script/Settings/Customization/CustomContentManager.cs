using System.Collections.Generic;
using System.IO;
using YARG.Helpers;
using YARG.Menu.Settings;
using YARG.Settings.Metadata;

namespace YARG.Settings.Customization
{
    public static class CustomContentManager
    {
        private const string CUSTOM_DIRECTORY = "custom";

        public static string CustomizationDirectory => Path.Combine(PathHelper.PersistentDataPath, CUSTOM_DIRECTORY);

        public static readonly ColorProfileContainer   ColorProfiles;
        public static readonly CameraSettingsContainer CameraSettings;

        private static readonly List<CustomContent> _customContentContainers;
        public static IReadOnlyList<CustomContent> CustomContentContainers => _customContentContainers;

        static CustomContentManager()
        {
            CameraSettings = new CameraSettingsContainer(Path.Combine(CustomizationDirectory, "cameras"));
            ColorProfiles = new ColorProfileContainer(Path.Combine(CustomizationDirectory, "colors"));

            _customContentContainers = new()
            {
                CameraSettings,
                ColorProfiles
            };
        }

        public static void Init()
        {
            ColorProfiles.LoadFiles();
            CameraSettings.LoadFiles();
        }

        public static void SaveAll()
        {
            foreach (var content in CustomContentContainers)
            {
                content.SaveAll();
            }
        }
    }
}