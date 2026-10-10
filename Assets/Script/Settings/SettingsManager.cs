using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Menu.Filters;
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
                new JsonUnityColorConverter(),
                new JsonColorConverter(),
                new JsonVector2Converter()
            }
        };

        /// <summary>
        /// Holds settings needed before the full settings list has been loaded.
        /// </summary>
        private sealed class StartupSettingValues
        {
            public string OutputDevice { get; set; }
        }

        private static string _serializedSettings;

        private static bool _settingsCanBeSaved = true;

        public static string OutputDeviceAtStartup { get; private set; } = "Default";

        public static SettingContainer Settings { get; private set; }

        public static readonly List<Tab> DisplayedSettingsTabs = new()
        {
            new MetadataTab("General", icon: "Engine")
            {
                new HeaderMetadata("Calibration"),
                new ButtonRowMetadata(nameof(Settings.OpenCalibrator)),
                nameof(Settings.AudioCalibration),
                nameof(Settings.VideoCalibration),
                new FieldMetadata(nameof(Settings.AccountForHardwareLatency), true),
                // Not sure how to use nameof?
                new HeaderMetadata("OffsetCalibration"),
                nameof(Settings.ShowMeanSongOffsetCalibration),
                nameof(Settings.UseSongOffsetCalibration),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata(nameof(Settings.OpenVenueFolder)),
                nameof(Settings.DisableDefaultBackground),
                nameof(Settings.DisableGlobalBackgrounds),
                nameof(Settings.DisablePerSongBackgrounds),
                nameof(Settings.WaitForSongVideo),
                nameof(Settings.AllowRemoteContent),

                new HeaderMetadata("Gameplay"),
                nameof(Settings.InputPollingFrequency),
                nameof(Settings.VoiceActivatedVocalStarPower),
                nameof(Settings.EnablePracticeSP),
                nameof(Settings.PracticeRestartDelay),
                nameof(Settings.NoFail),
                nameof(Settings.LearningGuides),
                nameof(Settings.ReduceNoteSpeedByDifficulty),

                new HeaderMetadata("StatusBar"),
                nameof(Settings.ShowBattery),
                nameof(Settings.ShowTime),
                nameof(Settings.MemoryStats),
                nameof(Settings.FpsStats),
                nameof(Settings.ShowActivePlayers),
                nameof(Settings.ShowActiveBots),

                new HeaderMetadata("Other"),
                nameof(Settings.ReconnectProfiles),
                nameof(Settings.AutoCreateProfiles),
                nameof(Settings.ShowCursorTimer),
                nameof(Settings.PauseOnDeviceDisconnect),
                nameof(Settings.PauseOnFocusLoss),
                nameof(Settings.PauseOnMenuOpen),
                nameof(Settings.MuteOnFocusLoss),
                nameof(Settings.WrapAroundNavigation),
                nameof(Settings.DiscordRichPresence),
                nameof(Settings.AmIAwesome),
            },
            new SongManagerTab("SongManager", icon: "Songs")
            {
                new HeaderMetadata("SongFolders"),
                new HeaderMetadata("ScanningOptions"),
                new FieldMetadata(nameof(Settings.AllowDuplicateSongs), requiresRescan: true),
                new FieldMetadata(nameof(Settings.UseFullDirectoryForPlaylists), requiresRescan: true),
                new FieldMetadata(nameof(Settings.Genrelizer), requiresRescan: true),
                new HeaderMetadata("LibraryDisplay"),
                nameof(Settings.ShowFavoriteButton),
                nameof(Settings.DifficultyRings),
                nameof(Settings.HighScoreInfo),
                nameof(Settings.HighScoreHistory),
                nameof(Settings.ShowPercentDecimals),
                new HeaderMetadata("SortingAndFiltering"),
                nameof(Settings.MaxSongRating),
                nameof(Settings.CensorMatureContent),
                nameof(Settings.RememberFilters),
                nameof(Settings.SecondaryAlbumSort),
                nameof(Settings.SongLengthLabels),
                new HeaderMetadata("PlayAShow"),
                nameof(Settings.EnablePlayAShow),
                nameof(Settings.PlayAShowTimeout),
                nameof(Settings.RequireAllDifficulties),
            },
            new MetadataTab("Sound", icon: "Sound")
            {
                new HeaderMetadata("Volume"),
                nameof(Settings.EnableNormalization),
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
                nameof(Settings.VenueSfxVolume),
                nameof(Settings.PreviewVolume),
                nameof(Settings.MusicPlayerVolume),
                nameof(Settings.VocalMonitoring),
                nameof(Settings.VocalReverb),
                nameof(Settings.MetronomeVolume),

                new HeaderMetadata("Customization"),
                nameof(Settings.AutomaticPlaybackBuffer),
                nameof(Settings.PlaybackBufferLength),

                new HeaderMetadata("Input"),
                nameof(Settings.MicrophoneSensitivity),

                new HeaderMetadata("Gameplay"),
                nameof(Settings.MuteOnMiss),
                nameof(Settings.MuteOnlyWhenAllPlayersMiss),
                nameof(Settings.UseStarpowerFx),
                nameof(Settings.UseVenueSfx),
                nameof(Settings.OverstrumAndOverhitSoundEffects),
                nameof(Settings.AlwaysOnDrumSFX),
                nameof(Settings.UseWhammyFx),
                nameof(Settings.WhammyPitchShiftAmount),

                new HeaderMetadata("CrowdFX"),
                nameof(Settings.UseCrowdCheering),
                nameof(Settings.UseCrowdIdle),
                nameof(Settings.UseStarPowerClaps),
                nameof(Settings.UsePerformanceClaps),

                new HeaderMetadata("Other"),
                nameof(Settings.EffectsMode),
                nameof(Settings.UseChipmunkSpeed),
                nameof(Settings.ApplyVolumesInMusicLibrary),
                nameof(Settings.ApplyVolumesInMusicPlayer),
                nameof(Settings.EnableVoxSamples),
                nameof(Settings.MetronomeSound),
            },
            new MetadataTab("Graphics", icon: "Display", new TrackPreviewBuilder())
            {
                new HeaderMetadata("Display"),
                nameof(Settings.VSync),
                nameof(Settings.FpsCap),
                nameof(Settings.VenueFpsCap),
                nameof(Settings.BackgroundFpsCap),
                nameof(Settings.FullscreenMode),
                nameof(Settings.Resolution),
                nameof(Settings.FpsStats),

                new HeaderMetadata("Graphics", showPreview: true),
                nameof(Settings.LowQuality),
                nameof(Settings.DisableBloom),
                nameof(Settings.DisableFilmGrain),
                nameof(Settings.StarPowerHighwayFx),
                nameof(Settings.SongBackgroundOpacity),
                nameof(Settings.VenueRenderingQuality),
                nameof(Settings.VenueAntiAliasing),
                nameof(Settings.VenuePostProcessing),
                nameof(Settings.ReduceFlashingLights),

                new HeaderMetadata("Gameplay", showPreview: true),
                nameof(Settings.StaticVocalsMode),
                nameof(Settings.UseThreeLaneLyricsInHarmony),
                nameof(Settings.EnableTrackEffects),
                nameof(Settings.EnableHighwayAnimation),
                nameof(Settings.KickBounceMultiplier),
                nameof(Settings.HighwayTiltMultiplier),

                new HeaderMetadata("HUD", showPreview: true),
                nameof(Settings.ShowHitWindow),
                nameof(Settings.DisableTextNotifications),
                nameof(Settings.NoteStreakFrequency),
                nameof(Settings.VocalStreakFrequency),
                nameof(Settings.CountdownDisplay),
                nameof(Settings.UnisonDisplay),
                nameof(Settings.ShowPlayerNameWhenStartingSong),
                nameof(Settings.LyricDisplay),
                nameof(Settings.KeepLyricBar),
                nameof(Settings.SongTimeOnScoreBox),
                nameof(Settings.GraphicalProgressOnScoreBox),
                nameof(Settings.GraphicalSongProgressTint),
                nameof(Settings.KeepSongInfoVisible),
            },
            new PresetsTab("Presets", icon: "Customization"),
            new AllSettingsTab(),
        };

        public static readonly List<Tab> AllSettingsTabs = new()
        {
            // The displayed tabs are appended to the top here

            new MetadataTab("FileManagement", icon: "Files")
            {
                new HeaderMetadata("ExportSongs"),
                new ButtonRowMetadata(
                    nameof(Settings.ExportSongsJson),
                    nameof(Settings.ExportSongsText),
                    nameof(Settings.ExportSongsCsv),
                    nameof(Settings.ExportSongsWeb)
                ),
                new HeaderMetadata("PathsAndFolders"),
                new ButtonRowMetadata(
                    nameof(Settings.CopyCurrentSongTextFilePath),
                    nameof(Settings.CopyCurrentSongJsonFilePath)),
                new ButtonRowMetadata(nameof(Settings.OpenPersistentDataPath)),
                new ButtonRowMetadata(nameof(Settings.OpenExecutablePath)),
                new HeaderMetadata("CacheManagement"),
                new ButtonRowMetadata(nameof(Settings.RemoveRemoteContent)),
            },
            new MetadataTab("LightingPeripherals", icon: "Lighting", new DMXInformationPanelBuilder())
            {
                new HeaderMetadata("LightingGeneral", showPreview: true),
                nameof(Settings.StageKitEnabled),
                nameof(Settings.DMXEnabled),
                nameof(Settings.RB3EEnabled),
                new HeaderMetadata("StageKitDMXChannels", showPreview: true),
                nameof(Settings.DMXDimmerChannels),
                nameof(Settings.DMXRedChannels),
                nameof(Settings.DMXGreenChannels),
                nameof(Settings.DMXBlueChannels),
                nameof(Settings.DMXYellowChannels),
                nameof(Settings.DMXFogChannels),
                nameof(Settings.DMXStrobeChannels),
                new HeaderMetadata("AdvancedDMXChannels", showPreview: true),
                nameof(Settings.DMXCueChangeChannel),
                nameof(Settings.DMXPostProcessingChannel),
                nameof(Settings.DMXKeyframeChannel),
                nameof(Settings.DMXBeatlineChannel),
                nameof(Settings.DMXBonusEffectChannel),
                nameof(Settings.DMXDrumsChannel),
                nameof(Settings.DMXGuitarChannel),
                nameof(Settings.DMXBassChannel),
                nameof(Settings.DMXKeysChannel),
                new HeaderMetadata("AdvancedDMXSettings", showPreview: true),
                nameof(Settings.DMXLocalIP),
                nameof(Settings.DMXUniverseChannel),
                nameof(Settings.DMXDimmerValues),
                nameof(Settings.DMXTargetFPS),
                nameof(Settings.DMXPulseDuration),

                //NYI
                //nameof(Settings.DMXPerformerChannel)
                new HeaderMetadata("RB3E", showPreview: true),
                nameof(Settings.RB3EBroadcastIP),

            },
            new MetadataTab("Debug", icon: "Debug")
            {
                nameof(Settings.InputDeviceLogging),
                nameof(Settings.ShowAdvancedMusicLibraryOptions),
                nameof(Settings.MinimumLogLevel),
            },
            new MetadataTab("Experimental", icon: "Beaker")
            {
                new HeaderMetadata("Other"),
                nameof(Settings.BandComboTypeSetting),
                nameof(Settings.DataStreamEnable),
                nameof(Settings.SaveScoresWithBots),
                new HeaderMetadata("Accessibility"),
                nameof(Settings.FontScaling),
                new HeaderMetadata("OutputConfiguration"),
                new FieldMetadata(nameof(Settings.OutputMode), visibleWhen: IsWindows),
                nameof(Settings.OutputDevice),
                new FieldMetadata(nameof(Settings.AsioBufferSize), visibleWhen: IsAsioVisible),
                new ButtonRowMetadata(nameof(Settings.OpenAsioControlPanel), IsAsioVisible),
                nameof(Settings.OutputChannelDefault),
                nameof(Settings.OutputChannelDrumSfx),
                nameof(Settings.OutputChannelMetronome),
                nameof(Settings.OutputChannelSfx),
                nameof(Settings.OutputChannelVox),
            }
        };

        static SettingsManager()
        {
            AllSettingsTabs.InsertRange(0, DisplayedSettingsTabs);
        }

        private static string SettingsFile => Path.Combine(PathHelper.PersistentDataPath, "settings.json");

        public static void LoadStartupSettings()
        {
            try
            {
                _serializedSettings = File.ReadAllText(SettingsFile);
                var startupSettings = JsonConvert.DeserializeObject<StartupSettingValues>(_serializedSettings);
                OutputDeviceAtStartup = startupSettings?.OutputDevice ?? "Default";
            }
            catch
            {
                // Full settings load reports file and JSON errors during normal startup.
                OutputDeviceAtStartup = "Default";
            }
        }

        private static bool IsWindows() => Application.platform is RuntimePlatform.WindowsPlayer or
            RuntimePlatform.WindowsEditor;

        private static bool IsAsioVisible() => IsWindows() && Settings?.OutputMode.Value == AudioOutputMode.Asio;

        public static void LoadSettings()
        {
            _settingsCanBeSaved = true;
            bool settingsFileExists = File.Exists(SettingsFile);

            // Create settings container
            try
            {
                string text = _serializedSettings ?? File.ReadAllText(SettingsFile);
                _serializedSettings = null;

                var settingsJson = JObject.Parse(text);
                settingsJson = SettingsMigration.Migrate(settingsJson, out _settingsCanBeSaved);
                Settings = JsonConvert.DeserializeObject<SettingContainer>(
                    settingsJson.ToString(Formatting.None), JsonSettings);
            }
            catch (Exception e)
            {
                _settingsCanBeSaved = !settingsFileExists;
                YargLogger.LogException(e, "Failed to load settings!");
            }

            // If null, recreate
            Settings ??= new SettingContainer();

            AudioOutputMode outputMode = GlobalAudioHandler.GetOutputMode(Settings.OutputDevice.Value);
            Settings.OutputMode.SetValueWithoutNotify(outputMode);
            Settings.RememberOutputDevice(outputMode, Settings.OutputDevice.Value);
            Settings.OutputDevice.UpdateValues(outputMode);
            Settings.AsioBufferSize.UpdateValues();

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
            if (SettingContainer.IsInitialized && Settings is not null && _settingsCanBeSaved)
            {
                FiltersMenu.PrepareForSettingsSave();
                var json = JObject.Parse(JsonConvert.SerializeObject(Settings, JsonSettings));
                SettingsMigration.SetCurrentSchemaVersion(json);
                File.WriteAllText(SettingsFile, json.ToString(Formatting.Indented));
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

            if (field != null)
            {
                var value = field.GetValue(Settings);

                if (value == null)
                {
                    YargLogger.LogFormatWarning("`{0}` has a value of null. This might create errors.", name);
                }

                return (ISettingType) value;
            }

            throw new Exception($"The field `{name}` does not exist.");
        }

        public static void InvokeButton(string name)
        {
            var method = typeof(SettingContainer).GetMethod(name);

            if (method != null)
            {
                method.Invoke(Settings, null);
            }
            else
            {
                throw new Exception($"The method `{name}` does not exist.");
            }
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
