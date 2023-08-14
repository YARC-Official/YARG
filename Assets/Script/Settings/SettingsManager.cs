using System;
using System.Collections;
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
        public class Tab : IEnumerable<AbstractMetadata>
        {
            public string Name { get; }
            public string Icon { get; }
            public string PreviewPath { get; }
            public bool ShowInPlayMode { get; }

            private List<AbstractMetadata> _settings = new();
            public IReadOnlyList<AbstractMetadata> Settings => _settings;

            public Tab(string name, string icon = "Generic", string previewPath = null, bool showInPlayMode = false)
            {
                Name = name;
                Icon = icon;
                PreviewPath = previewPath;
                ShowInPlayMode = showInPlayMode;
            }

            // For collection initializer support
            public void Add(AbstractMetadata setting) => _settings.Add(setting);
            public List<AbstractMetadata>.Enumerator GetEnumerator() => _settings.GetEnumerator();
            IEnumerator<AbstractMetadata> IEnumerable<AbstractMetadata>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public const string SONG_FOLDER_MANAGER_TAB = "_SongFolderManager";

        public static SettingContainer Settings { get; private set; }

        public static readonly List<Tab> SettingsTabs = new()
        {
            new(name: "General")
            {
                new HeaderMetadata("FileManagement"),
                new ButtonRowMetadata("OpenSongFolderManager"),
                new ButtonRowMetadata("ExportOuvertSongs"),
                new ButtonRowMetadata("CopyCurrentSongTextFilePath", "CopyCurrentSongJsonFilePath"),
                new HeaderMetadata("Venues"),
                new ButtonRowMetadata("OpenVenueFolder"),
                "DisablePerSongBackgrounds",
                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata("OpenCalibrator"),
                "AudioCalibration",
                new HeaderMetadata("Other"),
                "ShowHitWindow",
                "UseCymbalModelsInFiveLane",
                "KickBounce",
                "ShowCursorTimer",
                "PressThreshold",
                "AmIAwesome"
            },
            new(name: "Sound", icon: "Sound", showInPlayMode: true)
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
            new(name: "Graphics", icon: "Display", previewPath: "SettingPreviews/TrackPreview", showInPlayMode: true)
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
                new HeaderMetadata("Camera"),
                new PresetDropdownMetadata("CameraPresets", new[]
                {
                    "TrackCamFOV",
                    "TrackCamYPos",
                    "TrackCamZPos",
                    "TrackCamRot",
                    "TrackFadePosition",
                    "TrackFadeSize",
                    "TrackCurveFactor",
                }, new()
                {
                    new DropdownPreset("Default", new()
                    {
                        { "TrackCamFOV", 55f },
                        { "TrackCamYPos", 2.66f },
                        { "TrackCamZPos", 1.14f },
                        { "TrackCamRot", 24.12f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("High FOV", new()
                    {
                        { "TrackCamFOV", 60f },
                        { "TrackCamYPos", 2.66f },
                        { "TrackCamZPos", 1.27f },
                        { "TrackCamRot", 24.12f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("The Band 1", new()
                    {
                        { "TrackCamFOV", 47.84f },
                        { "TrackCamYPos", 2.43f },
                        { "TrackCamZPos", 1.42f },
                        { "TrackCamRot", 26f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("The Band 2", new()
                    {
                        { "TrackCamFOV", 44.97f },
                        { "TrackCamYPos", 2.66f },
                        { "TrackCamZPos", 0.86f },
                        { "TrackCamRot", 24.12f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("The Band 3", new()
                    {
                        { "TrackCamFOV", 57.29f },
                        { "TrackCamYPos", 2.22f },
                        { "TrackCamZPos", 1.61f },
                        { "TrackCamRot", 23.65f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("The Band 4", new()
                    {
                        { "TrackCamFOV", 62.16f },
                        { "TrackCamYPos", 2.56f },
                        { "TrackCamZPos", 1.20f },
                        { "TrackCamRot", 19.43f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("Hero 2", new()
                    {
                        { "TrackCamFOV", 58.15f },
                        { "TrackCamYPos", 1.82f },
                        { "TrackCamZPos", 1.50f },
                        { "TrackCamRot", 12.40f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.5f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("Hero 3", new()
                    {
                        { "TrackCamFOV", 52.71f },
                        { "TrackCamYPos", 2.17f },
                        { "TrackCamZPos", 1.14f },
                        { "TrackCamRot", 15.21f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.5f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("Hero Traveling the World", new()
                    {
                        { "TrackCamFOV", 53.85f },
                        { "TrackCamYPos", 1.97f },
                        { "TrackCamZPos", 1.52f },
                        { "TrackCamRot", 16.62f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.5f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("Hero Live", new()
                    {
                        { "TrackCamFOV", 62.16f },
                        { "TrackCamYPos", 2.40f },
                        { "TrackCamZPos", 1.42f },
                        { "TrackCamRot", 21.31f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.25f },
                        { "TrackCurveFactor", 0.5f },
                    }),
                    new DropdownPreset("Clone", new()
                    {
                        { "TrackCamFOV", 55f },
                        { "TrackCamYPos", 2.07f },
                        { "TrackCamZPos", 1.51f },
                        { "TrackCamRot", 17.09f },
                        { "TrackFadePosition", 3f },
                        { "TrackFadeSize", 1.5f },
                        { "TrackCurveFactor", 0.5f },
                    })
                }),
                "TrackCamFOV",
                "TrackCamYPos",
                "TrackCamZPos",
                "TrackCamRot",
                "TrackFadePosition",
                "TrackFadeSize",
                "TrackCurveFactor",
                new HeaderMetadata("Other"),
                "DisableTextNotifications",
                "LyricBackground"
            },
            new(name: "Engine", icon: "Gameplay")
            {
                "NoKicks",
                "AntiGhosting",
                "InfiniteFrontEnd"
            },
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