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
                new ButtonRowMetadata(
                    nameof(Settings.ExportSongsOuvert),
                    nameof(Settings.ExportSongsText)),
                new ButtonRowMetadata(
                    nameof(Settings.CopyCurrentSongTextFilePath),
                    nameof(Settings.CopyCurrentSongJsonFilePath)),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata(nameof(Settings.OpenVenueFolder)),
                nameof(Settings.DisablePerSongBackgrounds),

                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata(nameof(Settings.OpenCalibrator)),
                nameof(Settings.AudioCalibration),
                nameof(Settings.VideoCalibration),

                new HeaderMetadata("Other"),
                nameof(Settings.UseCymbalModelsInFiveLane),
                nameof(Settings.KickBounceMultiplier),
                nameof(Settings.ShowCursorTimer),
                nameof(Settings.AmIAwesome),

                new HeaderMetadata("Advanced"),
                nameof(Settings.InputDeviceLogging),
                nameof(Settings.ShowAdvancedMusicLibraryOptions)
            },
            new SongManagerTab("SongManager", icon: "Songs"),
            new MetadataTab("Sound", icon: "Sound")
            {
                new HeaderMetadata("Volume"),
                nameof(Settings.MasterMusicVolume),
                nameof(Settings.GuitarVolume),
                nameof(Settings.RhythmVolume),
                nameof(Settings.BassVolume),
                nameof(Settings.KeysVolume),
                nameof(Settings.DrumsVolume),
                nameof(Settings.VocalsVolume),
                nameof(Settings.SongVolume),
                nameof(Settings.CrowdVolume),
                nameof(Settings.SfxVolume),
                nameof(Settings.PreviewVolume),
                nameof(Settings.MusicPlayerVolume),
                nameof(Settings.VocalMonitoring),

                new HeaderMetadata("Input"),
                nameof(Settings.MicrophoneSensitivity),

                new HeaderMetadata("Other"),
                nameof(Settings.MuteOnMiss),
                nameof(Settings.UseStarpowerFx),
                // nameof(Settings.UseWhammyFx),
                // nameof(Settings.WhammyPitchShiftAmount),
                // nameof(Settings.WhammyOversampleFactor),
                // nameof(Settings.ClapsInStarpower),
                // nameof(Settings.ReverbInStarpower),
                nameof(Settings.UseChipmunkSpeed),
            },
            new MetadataTab("Graphics", icon: "Display", new TrackPreviewBuilder())
            {
                new HeaderMetadata("Display"),
                nameof(Settings.VSync),
                nameof(Settings.FpsCap),
                nameof(Settings.FullscreenMode),
                nameof(Settings.Resolution),
                nameof(Settings.FpsStats),

                new HeaderMetadata("Graphics"),
                nameof(Settings.LowQuality),
                nameof(Settings.DisableBloom),

                new HeaderMetadata("Other"),
                nameof(Settings.ShowHitWindow),
                nameof(Settings.DisableTextNotifications),
                nameof(Settings.LyricDisplay),
                nameof(Settings.SongTimeOnScoreBox),
                nameof(Settings.GraphicalProgressOnScoreBox)
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

            if (settingInfo.ValueType != value.GetType())
            {
                throw new Exception($"The setting `{name}` is of type {settingInfo.ValueType}, not {value.GetType()}.");
            }

            settingInfo.ValueAsObject = value;
        }
    }
}