using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
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
                new FieldMetadata(nameof(Settings.AccountForHardwareLatency), true, advanced: true),

                new HeaderMetadata("Venues"),
                new ButtonRowMetadata(nameof(Settings.OpenVenueFolder), advanced: true),
                new FieldMetadata(nameof(Settings.DisableDefaultBackground), advanced: true),
                new FieldMetadata(nameof(Settings.DisableGlobalBackgrounds), advanced: true),
                nameof(Settings.DisablePerSongBackgrounds),
                new FieldMetadata(nameof(Settings.WaitForSongVideo), advanced: true),

                new HeaderMetadata("Gameplay"),
                new FieldMetadata(nameof(Settings.InputPollingFrequency), advanced: true),
                nameof(Settings.VoiceActivatedVocalStarPower),
                new FieldMetadata(nameof(Settings.EnablePracticeSP), advanced: true),
                new FieldMetadata(nameof(Settings.PracticeRestartDelay), advanced: true),
                nameof(Settings.NoFailMode),
                nameof(Settings.LearningGuides),
                new FieldMetadata(nameof(Settings.ReduceNoteSpeedByDifficulty), advanced: true),

                new HeaderMetadata("StatusBar"),
                nameof(Settings.ShowBattery),
                nameof(Settings.ShowTime),
                new FieldMetadata(nameof(Settings.MemoryStats), advanced: true),
                new FieldMetadata(nameof(Settings.FpsStats), advanced: true),
                new FieldMetadata(nameof(Settings.ShowActivePlayers), advanced: true),
                new FieldMetadata(nameof(Settings.ShowActiveBots), advanced: true),

                new HeaderMetadata("Other"),
                nameof(Settings.ReconnectProfiles),
                nameof(Settings.AutoCreateProfiles),
                new FieldMetadata(nameof(Settings.ShowCursorTimer), advanced: true),
                nameof(Settings.PauseOnDeviceDisconnect),
                nameof(Settings.PauseOnFocusLoss),
                nameof(Settings.WrapAroundNavigation),
                nameof(Settings.DiscordRichPresence),
                new FieldMetadata(nameof(Settings.AmIAwesome), advanced: true),
            },
            new SongManagerTab("SongManager", icon: "Songs")
            {
                new HeaderMetadata("ScanningOptions"),
                nameof(Settings.AllowDuplicateSongs),
                nameof(Settings.UseFullDirectoryForPlaylists),
                nameof(Settings.StandardizeGenres),
                new HeaderMetadata("MusicLibrary"),
                nameof(Settings.ShowFavoriteButton),
                nameof(Settings.DifficultyRings),
                nameof(Settings.HighScoreInfo),
                nameof(Settings.HighScoreHistory),
                new HeaderMetadata("PlayAShow"),
                nameof(Settings.PlayAShowTimeout),
                nameof(Settings.RequireAllDifficulties),
            },
            new MetadataTab("Sound", icon: "Sound")
            {
                new HeaderMetadata("Volume"),
                nameof(Settings.MasterMusicVolume),
                nameof(Settings.GuitarVolume),
                new FieldMetadata(nameof(Settings.RhythmVolume), advanced: true),
                nameof(Settings.BassVolume),
                new FieldMetadata(nameof(Settings.KeysVolume), advanced: true),
                nameof(Settings.DrumsVolume),
                nameof(Settings.VocalsVolume),
                new FieldMetadata(nameof(Settings.SongVolume), advanced: true),
                new FieldMetadata(nameof(Settings.CrowdVolume), advanced: true),
                nameof(Settings.SfxVolume),
                new FieldMetadata(nameof(Settings.DrumSfxVolume), advanced: true),
                new FieldMetadata(nameof(Settings.PreviewVolume), advanced: true),
                nameof(Settings.MusicPlayerVolume),
                new FieldMetadata(nameof(Settings.VocalMonitoring), advanced: true),
                new FieldMetadata(nameof(Settings.MetronomeVolume), advanced: true),

                new HeaderMetadata("Customization", advanced: true),
                new FieldMetadata(nameof(Settings.EnablePlaybackBuffer), advanced: true),
                new FieldMetadata(nameof(Settings.PlaybackBufferLength), advanced: true),

                new HeaderMetadata("Input"),
                nameof(Settings.MicrophoneSensitivity),

                new HeaderMetadata("Gameplay"),
                nameof(Settings.MuteOnMiss),
                nameof(Settings.UseStarpowerFx),
                nameof(Settings.UseCrowdFx),
                nameof(Settings.OverstrumAndOverhitSoundEffects),
                new FieldMetadata(nameof(Settings.AlwaysOnDrumSFX), advanced: true),
                new FieldMetadata(nameof(Settings.UseWhammyFx), advanced: true),
                new FieldMetadata(nameof(Settings.WhammyPitchShiftAmount), advanced: true),

                new HeaderMetadata("Other"),
                new FieldMetadata(nameof(Settings.UseChipmunkSpeed), advanced: true),
                new FieldMetadata(nameof(Settings.ApplyVolumesInMusicLibrary), advanced: true),
                nameof(Settings.EnableVoxSamples),
                new FieldMetadata(nameof(Settings.MetronomeSound), advanced: true),
            },
            new MetadataTab("Graphics", icon: "Display", new TrackPreviewBuilder())
            {
                new HeaderMetadata("Display"),
                nameof(Settings.VSync),
                new FieldMetadata(nameof(Settings.FpsCap), advanced: true),
                new FieldMetadata(nameof(Settings.VenueFpsCap), advanced: true),
                nameof(Settings.FullscreenMode),
                nameof(Settings.Resolution),
                new FieldMetadata(nameof(Settings.FpsStats), advanced: true),

                new HeaderMetadata("Graphics"),
                nameof(Settings.LowQuality),
                new FieldMetadata(nameof(Settings.DisableBloom), advanced: true),
                new FieldMetadata(nameof(Settings.DisableFilmGrain), advanced: true),
                nameof(Settings.StarPowerHighwayFx),
                nameof(Settings.SongBackgroundOpacity),
                new FieldMetadata(nameof(Settings.VenueRenderingQuality), advanced: true),
                new FieldMetadata(nameof(Settings.VenueAntiAliasing), advanced: true),
                new FieldMetadata(nameof(Settings.VenuePostProcessing), advanced: true),

                new HeaderMetadata("Gameplay"),
                nameof(Settings.StaticVocalsMode),
                new FieldMetadata(nameof(Settings.UseThreeLaneLyricsInHarmony), advanced: true),
                nameof(Settings.EnableTrackEffects),
                nameof(Settings.EnableHighwayAnimation),
                new FieldMetadata(nameof(Settings.KickBounceMultiplier), advanced: true),
                new FieldMetadata(nameof(Settings.HighwayTiltMultiplier), advanced: true),

                new HeaderMetadata("HUD"),
                new FieldMetadata(nameof(Settings.ShowHitWindow), advanced: true),
                nameof(Settings.DisableTextNotifications),
                nameof(Settings.NoteStreakFrequency),
                new FieldMetadata(nameof(Settings.VocalStreakFrequency), advanced: true),
                nameof(Settings.CountdownDisplay),
                nameof(Settings.ShowPlayerNameWhenStartingSong),
                nameof(Settings.LyricDisplay),
                nameof(Settings.SongTimeOnScoreBox),
                nameof(Settings.GraphicalProgressOnScoreBox),
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
                    nameof(Settings.ExportSongsCsv)
                ),
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
            // new MetadataTab("Experimental", icon: "Beaker", new ExperimentalPreviewBuilder())
            new MetadataTab("Experimental", icon: "Beaker", new CharacterPreviewBuilder())
            {
                new HeaderMetadata("Other"),
                nameof(Settings.BandComboTypeSetting),
                nameof(Settings.CustomVocalsCharacter),
                nameof(Settings.DataStreamEnable),
                nameof(Settings.EnableNormalization),
                nameof(Settings.SaveScoresWithBots),
                new HeaderMetadata("Accessibility"),
                nameof(Settings.FontScaling),
                new HeaderMetadata("OutputConfiguration"),
                nameof(Settings.OutputDevice),
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
