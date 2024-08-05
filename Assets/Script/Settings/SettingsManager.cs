using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new JsonColorConverter(),
                new JsonVector2Converter()
            }
        };

        public static SettingContainer Settings { get; private set; }

        public static readonly List<Tab> DisplayedSettingsTabs = new()
        {
            new MetadataTab("General", icon: "Engine")
            {
                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata(nameof(Settings.OpenCalibrator)),
                nameof(Settings.AudioCalibration),
                nameof(Settings.VideoCalibration),
                nameof(Settings.AccountForHardwareLatency),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata(nameof(Settings.OpenVenueFolder)),
                nameof(Settings.DisableGlobalBackgrounds),
                nameof(Settings.DisablePerSongBackgrounds),
                nameof(Settings.WaitForSongVideo),

                new HeaderMetadata("StatusBar"),
                nameof(Settings.ShowBattery),
                nameof(Settings.ShowTime),
                nameof(Settings.MemoryStats),
                nameof(Settings.FpsStats),

                new HeaderMetadata("Other"),
                nameof(Settings.ReconnectProfiles),
                nameof(Settings.UseCymbalModelsInFiveLane),
                nameof(Settings.KickBounceMultiplier),
                nameof(Settings.ShowCursorTimer),
                nameof(Settings.PauseOnDeviceDisconnect),
                nameof(Settings.PauseOnFocusLoss),
                nameof(Settings.WrapAroundNavigation),
                nameof(Settings.AmIAwesome),
            },
            new SongManagerTab("SongManager", icon: "Songs")
            {
                new HeaderMetadata("ScanningOptions"),
                nameof(Settings.AllowDuplicateSongs),
                nameof(Settings.UseFullDirectoryForPlaylists),
                new HeaderMetadata("MusicLibrary"),
                nameof(Settings.ShowFavoriteButton),
                nameof(Settings.HighScoreInfo)
            },
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
                nameof(Settings.DrumSfxVolume),
                nameof(Settings.PreviewVolume),
                nameof(Settings.MusicPlayerVolume),
                nameof(Settings.VocalMonitoring),

                new HeaderMetadata("Customization"),
                nameof(Settings.EnablePlaybackBuffer),
                nameof(Settings.PlaybackBufferLength),

                new HeaderMetadata("Input"),
                nameof(Settings.MicrophoneSensitivity),

                new HeaderMetadata("Other"),
                nameof(Settings.MuteOnMiss),
                nameof(Settings.UseStarpowerFx),
                // nameof(Settings.UseWhammyFx),
                // nameof(Settings.WhammyPitchShiftAmount),
                // nameof(Settings.WhammyOversampleFactor),
                nameof(Settings.ClapsInStarpower),
                nameof(Settings.OverstrumAndOverhitSoundEffects),
                // nameof(Settings.ReverbInStarpower),
                nameof(Settings.UseChipmunkSpeed),
                nameof(Settings.ApplyVolumesInMusicLibrary),
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
                nameof(Settings.StarPowerHighwayFx),

                new HeaderMetadata("Other"),
                nameof(Settings.ShowHitWindow),
                nameof(Settings.DisableTextNotifications),
                nameof(Settings.EnablePracticeSP),
                nameof(Settings.NoteStreakFrequency),
                nameof(Settings.LyricDisplay),
                nameof(Settings.UpcomingLyricsTime),
                nameof(Settings.SongTimeOnScoreBox),
                nameof(Settings.GraphicalProgressOnScoreBox),
                nameof(Settings.KeepSongInfoVisible),
                nameof(Settings.CountdownDisplay)
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
            new MetadataTab("LightingPeripherals", icon: "Lighting", new DMXInformationPanelBuilder())
            {
                new HeaderMetadata("LightingGeneral"),
                nameof(Settings.StageKitEnabled),
                nameof(Settings.DMXEnabled),
                nameof(Settings.RB3EEnabled),
                new HeaderMetadata("StageKitDMXChannels"),
                nameof(Settings.DMXDimmerChannels),
                nameof(Settings.DMXRedChannels),
                nameof(Settings.DMXGreenChannels),
                nameof(Settings.DMXBlueChannels),
                nameof(Settings.DMXYellowChannels),
                nameof(Settings.DMXFogChannels),
                nameof(Settings.DMXStrobeChannels),
                new HeaderMetadata("AdvancedDMXChannels"),
                nameof(Settings.DMXCueChangeChannel),
                nameof(Settings.DMXPostProcessingChannel),
                nameof(Settings.DMXKeyframeChannel),
                nameof(Settings.DMXBeatlineChannel),
                nameof(Settings.DMXBonusEffectChannel),
                nameof(Settings.DMXDrumsChannel),
                nameof(Settings.DMXGuitarChannel),
                nameof(Settings.DMXBassChannel),
                nameof(Settings.DMXKeysChannel),
                new HeaderMetadata("AdvancedDMXSettings"),
                nameof(Settings.DMXUniverseChannel),
                nameof(Settings.DMXDimmerValues),
                //NYI
                //nameof(Settings.DMXPerformerChannel)
                new HeaderMetadata("RB3E"),
                nameof(Settings.RB3EBroadcastIP),

            },
            new MetadataTab("Debug", icon: "Debug")
            {
                nameof(Settings.InputDeviceLogging),
                nameof(Settings.ShowAdvancedMusicLibraryOptions),
                nameof(Settings.MinimumLogLevel),
            },
            new MetadataTab("Experimental", icon: "Beaker", new ExperimentalPreviewBuilder())
            {
                new HeaderMetadata("Other"),
                // Add experimental settings here
            }
        };

        static SettingsManager()
        {
            AllSettingsTabs.InsertRange(0, DisplayedSettingsTabs);
        }

        private static string SettingsFile => Path.Combine(PathHelper.PersistentDataPath, "settings.json");

        public static void LoadSettings()
        {
            // Create settings container
            try
            {
                string text = File.ReadAllText(SettingsFile);
                Settings = JsonConvert.DeserializeObject<SettingContainer>(text, JsonSettings);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load settings!");
            }

            // If null, recreate
            Settings ??= new SettingContainer();
            SettingContainer.IsInitialized = true;

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
            if (SettingContainer.IsInitialized && Settings is not null)
            {
                var json = JsonConvert.SerializeObject(Settings, JsonSettings);
                File.WriteAllText(SettingsFile, json);
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
                YargLogger.LogException(e, "Failed to delete settings!");
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
                YargLogger.LogFormatWarning("`{0}` has a value of null. This might create errors.", name);
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