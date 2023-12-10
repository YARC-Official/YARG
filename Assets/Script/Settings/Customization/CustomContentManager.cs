using System.Collections.Generic;
using System.IO;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public static class CustomContentManager
    {
        private const string CUSTOM_DIRECTORY = "custom";

        public static string CustomizationDirectory => Path.Combine(PathHelper.PersistentDataPath, CUSTOM_DIRECTORY);

        public static readonly ColorProfileContainer   ColorProfiles;
        public static readonly CameraSettingsContainer CameraSettings;
        public static readonly ThemePresetContainer    ThemePresets;
        public static readonly EnginePresetContainer   EnginePresets;

        private static readonly List<CustomContent> _customContentContainers;
        public static IReadOnlyList<CustomContent> CustomContentContainers => _customContentContainers;

        static CustomContentManager()
        {
            CameraSettings = new CameraSettingsContainer();
            ColorProfiles = new ColorProfileContainer();
            ThemePresets = new ThemePresetContainer();
            EnginePresets = new EnginePresetContainer();

            _customContentContainers = new()
            {
                CameraSettings,
                ColorProfiles,
                ThemePresets,
                EnginePresets
            };
        }

        public static void Initialize()
        {
            foreach (var content in CustomContentContainers)
            {
                content.Initialize();
            }
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