using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        public static SettingContainer Settings { get; private set; }

        public static readonly List<Tab> SettingsTabs = new()
        {
            new MetadataTab("General")
            {
                new HeaderMetadata("FileManagement"),
                new ButtonRowMetadata("ExportSongsOuvert", "ExportSongsText"),
                new ButtonRowMetadata("CopyCurrentSongTextFilePath", "CopyCurrentSongJsonFilePath"),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata("OpenVenueFolder"),
                "DisablePerSongBackgrounds",

                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata("OpenCalibrator"),
                "AudioCalibration",
                "VideoCalibration",

                new HeaderMetadata("Other"),
                "UseCymbalModelsInFiveLane",
                "KickBounce",
                "ShowCursorTimer",
                "AmIAwesome",

                new HeaderMetadata("Debug"),
                "InputDeviceLogging"
            },
            new SongManagerTab("SongManager", icon: "Songs"),
            new MetadataTab("Sound", icon: "Sound")
            {
                new HeaderMetadata("Volume"),
                "MasterMusicVolume",
                "GuitarVolume",
                "RhythmVolume",
                "BassVolume",
                "KeysVolume",
                "DrumsVolume",
                "VocalsVolume",
                "SongVolume",
                "CrowdVolume",
                "SfxVolume",
                "PreviewVolume",
                "MusicPlayerVolume",
                "VocalMonitoring",

                new HeaderMetadata("Input"),
                "MicrophoneSensitivity",

                new HeaderMetadata("Other"),
                "MuteOnMiss",
                "UseStarpowerFx",
                // "UseWhammyFx",
                // "WhammyPitchShiftAmount",
                // "WhammyOversampleFactor",
                // "ClapsInStarpower",
                // "ReverbInStarpower",
                "UseChipmunkSpeed",
            },
            new MetadataTab("Graphics", icon: "Display", new TrackPreviewBuilder())
            {
                new HeaderMetadata("Display"),
                "VSync",
                "FpsCap",
                "FullscreenMode",
                "Resolution",
                "FpsStats",

                new HeaderMetadata("Graphics"),
                "LowQuality",
                "DisableBloom",

                new HeaderMetadata("Other"),
                "ShowHitWindow",
                "DisableTextNotifications",
                "LyricDisplay",
                "SongTimeOnScoreBox",
                "GraphicalProgressOnScoreBox"
            },
            new MetadataTab("Engine", icon: "Engine")
            {
                // "NoKicks",
                "AntiGhosting",
                "InfiniteFrontEnd",
                "DynamicWindow"
            },
            new PresetsTab("Presets", icon: "Customization")
        };

        private static string SettingsFile => Path.Combine(PathHelper.PersistentDataPath, "settings.json");

        public static void LoadSettings()
        {
            SettingContainer.IsLoading = true;

            // Create settings container
            try
            {
                Settings = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(SettingsFile));
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load settings!");
                Debug.LogException(e);
            }

            // If null, recreate
            Settings ??= new SettingContainer();

            SettingContainer.IsLoading = false;

            // Now that we're done loading, call all of the callbacks
            var fields = typeof(SettingContainer).GetProperties();
            foreach (var field in fields)
            {
                var value = field.GetValue(Settings);

                if (value is not ISettingType settingType)
                {
                    continue;
                }

                settingType.ForceInvokeCallback();
            }
        }

        public static void SaveSettings()
        {
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        public static void DeleteSettings()
        {
            try
            {
                File.Delete(SettingsFile);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to delete settings!");
                Debug.LogException(e);
            }
        }

        public static ISettingType GetSettingByName(string name)
        {
            var field = typeof(SettingContainer).GetProperty(name);

            if (field == null)
            {
                throw new Exception($"The field `{name}` does not exist.");
            }

            var value = field.GetValue(Settings);

            if (value == null)
            {
                Debug.LogWarning($"`{name}` has a value of null. This might create errors.");
            }

            return (ISettingType) value;
        }

        public static void InvokeButton(string name)
        {
            var method = typeof(SettingContainer).GetMethod(name);

            if (method == null)
            {
                throw new Exception($"The method `{name}` does not exist.");
            }

            method.Invoke(Settings, null);
        }

        public static Tab GetTabByName(string name)
        {
            return SettingsTabs.FirstOrDefault(tab => tab.Name == name);
        }

        public static void SetSettingsByName(string name, object value)
        {
            var settingInfo = GetSettingByName(name);

            if (settingInfo.DataType != value.GetType())
            {
                throw new Exception($"The setting `{name}` is of type {settingInfo.DataType}, not {value.GetType()}.");
            }

            settingInfo.DataAsObject = value;
        }
    }
}