using System;
using System.Collections.Generic;
using System.IO;

using CameraSettingsData = YARG.Settings.Customization.CameraSettings;

namespace YARG.Settings.Customization
{
    public static partial class CustomContentManager
    {
        public static class CameraSettings
        {
            private const string CAMERA_SETTINGS_FOLDER = "cameraSettings";
            private static string CameraSettingsDirectory => Path.Combine(CustomizationDirectory, CAMERA_SETTINGS_FOLDER);

            private static readonly Dictionary<string, CameraSettingsData> _settings = new();
            public static IReadOnlyDictionary<string, CameraSettingsData> Settings => _settings;

            public static void Add(string name, CameraSettingsData settings)
            {
                if (_settings.ContainsKey(name))
                    throw new ArgumentException($"A camera setting preset already exists under the name of {settings.Name}!");

                _settings.Add(name, settings);
            }

            public static void Remove(string name)
            {
                _settings.Remove(name);
            }

            public static void Load() => LoadFiles(_settings, CameraSettingsDirectory);

            public static void Save(CameraSettingsData settings)
            {
                SaveItem(settings, settings.Name, CameraSettingsDirectory);
            }
        }
    }
}