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

        public static readonly List<Tab> DisplayedSettingsTabs = new()
        {
            new MetadataTab("General", icon: "Engine")
            {
                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata(nameof(Settings.OpenCalibrator)),
                nameof(Settings.AudioCalibration),
                nameof(Settings.VideoCalibration),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata(nameof(Settings.OpenVenueFolder)),
                nameof(Settings.DisableGlobalBackgrounds),
                nameof(Settings.DisablePerSongBackgrounds),

                new HeaderMetadata("Other"),
                nameof(Settings.UseCymbalModelsInFiveLane),
                nameof(Settings.KickBounceMultiplier),
                nameof(Settings.ShowCursorTimer),
                nameof(Settings.AmIAwesome),
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
                nameof(Settings.ClapsInStarpower),
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
                nameof(Settings.GraphicalProgressOnScoreBox),
                nameof(Settings.KeepSongInfoVisible)
            },
            new PresetsTab("Presets", icon: "Customization"),
            new AllSettingsTab(),
        };

        public static readonly List<Tab> AllSettingsTabs = new()
        {
            // The displayed tabs are appended to the top here

            new MetadataTab("FileManagement", icon: "Files")
            {
                new HeaderMetadata("Export"),
                new ButtonRowMetadata(
                    nameof(Settings.ExportSongsOuvert),
                    nameof(Settings.ExportSongsText)),
                new HeaderMetadata("PathsAndFolders"),
                new ButtonRowMetadata(
                    nameof(Settings.CopyCurrentSongTextFilePath),
                    nameof(Settings.CopyCurrentSongJsonFilePath)),
                new ButtonRowMetadata(nameof(Settings.OpenPersistentDataPath)),
                new ButtonRowMetadata(nameof(Settings.OpenExecutablePath)),
            },
            new MetadataTab("LightingPeripherals", icon: "Lighting")
            {
                new HeaderMetadata("LightingGeneral"),
                nameof(Settings.StageKitEnabled),
                nameof(Settings.LogitechEnabled),
                nameof(Settings.DMXEnabled),
                new HeaderMetadata("DMXChannels"),
                nameof(Settings.DMXDimmerChannels),
                nameof(Settings.DMXRedChannels),
                nameof(Settings.DMXGreenChannels),
                nameof(Settings.DMXBlueChannels),
                nameof(Settings.DMXYellowChannels),
                nameof(Settings.DMXFogChannel),
                nameof(Settings.DMXStrobeChannel),
            },
            new MetadataTab("Debug", icon: "Debug")
            {
                nameof(Settings.InputDeviceLogging),
                nameof(Settings.ShowAdvancedMusicLibraryOptions)
            }
        };

        static SettingsManager()
        {
            AllSettingsTabs.InsertRange(0, DisplayedSettingsTabs);
        }

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
            // If the game tries to save the settings before they are loaded, it can wipe the settings file
            // (such as closing the game before they load)
            if (SettingContainer.IsLoading || Settings is not null)
            {
                File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
            }
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
            return AllSettingsTabs.FirstOrDefault(tab => tab.Name == name);
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