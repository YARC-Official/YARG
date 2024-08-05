using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Helpers;
using YARG.Integration;
using YARG.Integration.RB3E;
using YARG.Integration.Sacn;
using YARG.Integration.StageKit;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Player;
using YARG.Settings.Types;
using YARG.Song;
using YARG.Venue;

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        public class SettingContainer
        {
            /// <summary>
            /// Have the settings been initialized?
            /// </summary>
            public static bool IsInitialized = false;

            #region Hidden Settings

            public List<string> SongFolders = new();

            public bool ShowAntiPiracyDialog = true;
            public bool ShowEngineInconsistencyDialog = true;
            public bool ShowExperimentalWarningDialog = true;

            public SortAttribute LibrarySort = SortAttribute.Name;

            public Dictionary<string, HUDPositionProfile> HUDPositionProfiles = new();

            #endregion

            #region General

            public void OpenCalibrator()
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Calibration);
                SettingsMenu.Instance.gameObject.SetActive(false);
            }

            public IntSetting AudioCalibration { get; } = new(0);
            public IntSetting VideoCalibration { get; } = new(0);

            public ToggleSetting AccountForHardwareLatency { get; } = new(true);

            public void OpenVenueFolder()
            {
                FileExplorerHelper.OpenFolder(VenueLoader.VenueFolder.FullName);
            }

            public ToggleSetting DisableGlobalBackgrounds { get; } = new(false);
            public ToggleSetting DisablePerSongBackgrounds { get; } = new(false);
            public ToggleSetting WaitForSongVideo { get; } = new(true);

            public ToggleSetting ShowBattery { get; } = new(false);
            public ToggleSetting ShowTime { get; } = new(false, ShowTimeCallback);
            public ToggleSetting MemoryStats { get; } = new(false, MemoryStatsCallback);

            public ToggleSetting ReconnectProfiles { get; } = new(true);

            public ToggleSetting UseCymbalModelsInFiveLane { get; } = new(true);
            public SliderSetting KickBounceMultiplier { get; } = new(1f, 0f, 2f);

            public SliderSetting ShowCursorTimer { get; } = new(2f, 0f, 5f);

            public ToggleSetting PauseOnDeviceDisconnect { get; } = new(true);
            public ToggleSetting PauseOnFocusLoss { get; } = new(true);

            public ToggleSetting WrapAroundNavigation { get; } = new(true);
            public ToggleSetting AmIAwesome { get; } = new(false);

            #endregion

            #region Songs

            public ToggleSetting AllowDuplicateSongs { get; } = new(true);
            public ToggleSetting UseFullDirectoryForPlaylists { get; } = new(false);

            public ToggleSetting ShowFavoriteButton { get; } = new(true);

            public DropdownSetting<HighScoreInfoMode> HighScoreInfo { get; }
                = new(HighScoreInfoMode.Stars)
                {
                    HighScoreInfoMode.Stars,
                    HighScoreInfoMode.Score,
                    HighScoreInfoMode.Off
                };

            #endregion

            #region Sound

            public VolumeSetting MasterMusicVolume { get; } = new(0.75f, v => GlobalAudioHandler.SetMasterVolume(v));

            public VolumeSetting GuitarVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Guitar, v));

            public VolumeSetting RhythmVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Rhythm, v));

            public VolumeSetting BassVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Bass, v));

            public VolumeSetting KeysVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Keys, v));

            public VolumeSetting DrumsVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Drums, v));

            public VolumeSetting VocalsVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Vocals, v));

            public VolumeSetting SongVolume { get; } =
                new(1f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Song, v));

            public VolumeSetting CrowdVolume { get; } =
                new(0.5f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Crowd, v));

            public VolumeSetting SfxVolume { get; } =
                new(0.8f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.Sfx, v));

            public VolumeSetting DrumSfxVolume { get; } =
                new(0.8f, v => GlobalAudioHandler.SetVolumeSetting(SongStem.DrumSfx, v));

            public VolumeSetting PreviewVolume { get; } = new(0.25f);
            public VolumeSetting MusicPlayerVolume { get; } = new(0.15f, MusicPlayerVolumeCallback);
            public VolumeSetting VocalMonitoring { get; } = new(0.7f, VocalMonitoringCallback);

            public ToggleSetting EnablePlaybackBuffer { get; } = new(true, GlobalAudioHandler.TogglePlaybackBuffer);

            public IntSetting PlaybackBufferLength { get; }
                = new(75, GlobalAudioHandler.MinimumBufferLength, GlobalAudioHandler.MaximumBufferLength,
                    GlobalAudioHandler.SetBufferLength);

            public SliderSetting MicrophoneSensitivity { get; } = new(2f, -50f, 50f);

            public DropdownSetting<AudioFxMode> MuteOnMiss { get; } = new(AudioFxMode.MultitrackOnly)
            {
                AudioFxMode.Off,
                AudioFxMode.MultitrackOnly,
                AudioFxMode.On
            };

            public DropdownSetting<AudioFxMode> UseStarpowerFx { get; } = new(AudioFxMode.On)
            {
                AudioFxMode.Off,
                AudioFxMode.MultitrackOnly,
                AudioFxMode.On
            };

            public ToggleSetting ClapsInStarpower { get; } = new(true);

            public ToggleSetting OverstrumAndOverhitSoundEffects { get; } = new(true);

            // public ToggleSetting UseWhammyFx            { get; } = new(true, UseWhammyFxChange);
            // public SliderSetting WhammyPitchShiftAmount { get; } = new(1, 1, 12, WhammyPitchShiftAmountChange);
            // public IntSetting    WhammyOversampleFactor { get; } = new(8, 4, 32, WhammyOversampleFactorChange);
            public ToggleSetting UseChipmunkSpeed { get; } = new(false, UseChipmunkSpeedChange);

            public ToggleSetting ApplyVolumesInMusicLibrary { get; } = new(true);

            #endregion

            #region Graphics

            public ToggleSetting VSync { get; } = new(true, VSyncCallback);
            public IntSetting FpsCap { get; } = new(60, 1, onChange: FpsCapCallback);

            public DropdownSetting<FullScreenMode> FullscreenMode { get; }
                = new(FullScreenMode.FullScreenWindow, FullscreenModeCallback)
                {
#if UNITY_STANDALONE_WIN
                    FullScreenMode.ExclusiveFullScreen,
#elif UNITY_STANDALONE_OSX
                FullScreenMode.MaximizedWindow,
#endif
                    FullScreenMode.FullScreenWindow,
                    FullScreenMode.Windowed,
                };

            public ResolutionSetting Resolution { get; } = new(ResolutionCallback);
            public ToggleSetting FpsStats { get; } = new(false, FpsCounterCallback);

            public ToggleSetting LowQuality { get; } = new(false, LowQualityCallback);
            public ToggleSetting DisableBloom { get; } = new(false, DisableBloomCallback);

            public DropdownSetting<StarPowerHighwayFxMode> StarPowerHighwayFx { get; }
                = new(StarPowerHighwayFxMode.On)
                {
                    StarPowerHighwayFxMode.On,
                    StarPowerHighwayFxMode.Reduced,
                    StarPowerHighwayFxMode.Off
                };

            public ToggleSetting ShowHitWindow { get; } = new(false, ShowHitWindowCallback);
            public ToggleSetting DisableTextNotifications { get; } = new(false);
            public ToggleSetting EnablePracticeSP { get; } = new(false);

            public DropdownSetting<NoteStreakFrequencyMode> NoteStreakFrequency { get; }
                = new(NoteStreakFrequencyMode.Frequent)
                {
                    NoteStreakFrequencyMode.Frequent,
                    NoteStreakFrequencyMode.Sparse,
                    NoteStreakFrequencyMode.Disabled
                };

            public DropdownSetting<SongProgressMode> SongTimeOnScoreBox { get; }
                = new(SongProgressMode.CountUpOnly)
                {
                    SongProgressMode.None,
                    SongProgressMode.CountUpAndTotal,
                    SongProgressMode.CountDownAndTotal,
                    SongProgressMode.CountUpOnly,
                    SongProgressMode.CountDownOnly,
                    SongProgressMode.TotalOnly
                };

            public ToggleSetting GraphicalProgressOnScoreBox { get; } = new(true);

            public DropdownSetting<LyricDisplayMode> LyricDisplay { get; }
                = new(LyricDisplayMode.Normal)
                {
                    LyricDisplayMode.Normal,
                    LyricDisplayMode.Transparent,
                    LyricDisplayMode.NoBackground,
                    LyricDisplayMode.Disabled
                };

            public SliderSetting UpcomingLyricsTime { get; } = new(3f, 0f, 10f);

            public ToggleSetting KeepSongInfoVisible { get; } = new(false);

            public DropdownSetting<CountdownDisplayMode> CountdownDisplay { get; }
                = new(CountdownDisplayMode.Measures)
                {
                    CountdownDisplayMode.Measures,
                    CountdownDisplayMode.Seconds,
                    CountdownDisplayMode.Disabled
                };

            #endregion

            #region File Management

            public void ExportSongsOuvert()
            {
                FileExplorerHelper.OpenSaveFile(null, "songs", "json", SongExport.ExportOuvert);
            }

            public void ExportSongsText()
            {
                FileExplorerHelper.OpenSaveFile(null, "songs", "txt", SongExport.ExportText);
            }

            public void CopyCurrentSongTextFilePath()
            {
                GUIUtility.systemCopyBuffer = CurrentSongController.TextFilePath;
            }

            public void CopyCurrentSongJsonFilePath()
            {
                GUIUtility.systemCopyBuffer = CurrentSongController.JsonFilePath;
            }

            public void OpenPersistentDataPath()
            {
                FileExplorerHelper.OpenFolder(PathHelper.PersistentDataPath);
            }

            public void OpenExecutablePath()
            {
                FileExplorerHelper.OpenFolder(PathHelper.ExecutablePath);
            }

            #endregion

            #region Lighting Peripherals

            public ToggleSetting StageKitEnabled { get; } = new(true, StageKitEnabledCallback);
            public ToggleSetting DMXEnabled { get; } = new(false, DMXEnabledCallback);
            public ToggleSetting RB3EEnabled { get; } = new(false, RB3EEnabledCallback);

            public DMXChannelsSetting DMXDimmerChannels { get; } = new(
                new[] { 01, 09, 17, 25, 33, 41, 49, 57 }, v => SacnInterpreter.Instance.DimmerChannels = v);

            public DMXChannelsSetting DMXRedChannels { get; } = new(
                new[] { 02, 10, 18, 26, 34, 42, 50, 58 }, v => SacnInterpreter.Instance.RedChannels = v);

            public DMXChannelsSetting DMXGreenChannels { get; } = new(
                new[] { 03, 11, 19, 27, 35, 43, 51, 59 }, v => SacnInterpreter.Instance.GreenChannels = v);

            public DMXChannelsSetting DMXBlueChannels { get; } = new(
                new[] { 04, 12, 20, 28, 36, 44, 52, 60 }, v => SacnInterpreter.Instance.BlueChannels = v);

            public DMXChannelsSetting DMXYellowChannels { get; } = new(
                new[] { 05, 13, 21, 29, 37, 45, 53, 61 }, v => SacnInterpreter.Instance.YellowChannels = v);

            public DMXChannelsSetting DMXFogChannels { get; } = new(
                new[] { 06, 14, 22, 30, 38, 46, 54, 62 }, v => SacnInterpreter.Instance.FogChannels = v);

            public DMXChannelsSetting DMXStrobeChannels { get; } = new(
                new[] { 07, 15, 23, 31, 39, 47, 55, 63 }, v => SacnInterpreter.Instance.StrobeChannels = v);

            public IntSetting DMXCueChangeChannel { get; } =
                new(8, 1, 512, v => SacnInterpreter.Instance.CueChangeChannel = v);

            public IPv4Setting RB3EBroadcastIP { get; } =
                new("255.255.255.255", ip => RB3EHardware.Instance.IPAddress = IPAddress.Parse(ip));

            public IntSetting DMXBeatlineChannel { get; } =
                new(14, 1, 512, v => SacnInterpreter.Instance.BeatlineChannel = v);

            public IntSetting DMXBonusEffectChannel { get; } =
                new(15, 1, 512, v => SacnInterpreter.Instance.BonusEffectChannel = v);

            public IntSetting DMXKeyframeChannel { get; } =
                new(16, 1, 512, v => SacnInterpreter.Instance.KeyframeChannel = v);

            public IntSetting DMXDrumsChannel { get; } =
                new(22, 1, 512, v => SacnInterpreter.Instance.DrumChannel = v);

            public IntSetting DMXPostProcessingChannel { get; } =
                new(23, 1, 512, v => SacnInterpreter.Instance.PostProcessingChannel = v);

            public IntSetting DMXGuitarChannel { get; } =
                new(24, 1, 512, v => SacnInterpreter.Instance.GuitarChannel = v);

            public IntSetting DMXBassChannel { get; } = new(30, 1, 512, v => SacnInterpreter.Instance.BassChannel = v);

            //NYI
            //public IntSetting DMXPerformerChannel { get; } = new(31, 1, 512);

            public IntSetting DMXKeysChannel { get; } = new(32, 1, 512, v => SacnInterpreter.Instance.KeysChannel = v);

            public IntSetting DMXUniverseChannel { get; } = new(1, 1, 65535);

            public DMXChannelsSetting DMXDimmerValues { get; } = new(new[] { 255, 255, 255, 255, 255, 255, 255, 255 });

            #endregion

            #region Debug and Developer

            public ToggleSetting InputDeviceLogging { get; } = new(false, InputDeviceLoggingCallback);

            public ToggleSetting ShowAdvancedMusicLibraryOptions { get; } = new(false);

            public DropdownSetting<LogLevel> MinimumLogLevel { get; } = new(
#if UNITY_EDITOR
                LogLevel.Debug,
#else
                LogLevel.Info,
#endif
                SetLogLevelCallback
            )
            {
                LogLevel.Trace,
                LogLevel.Debug,
                LogLevel.Info,
                LogLevel.Warning,
                LogLevel.Error,
                // No real need to distinguish these two,
                // they're very important to have in logs regardless
                // LogLevel.Exception,
                // LogLevel.Failure,
            };

            #endregion

            #region Callbacks

            private static void SetLogLevelCallback(LogLevel level)
            {
                YargLogger.MinimumLogLevel = level;
            }

            private static void ShowTimeCallback(bool value)
            {
                StatsManager.Instance.SetShowing(StatsManager.Stat.Time, value);
            }

            private static void MemoryStatsCallback(bool value)
            {
#if UNITY_EDITOR
                // Force in editor
                value = true;
#endif

                StatsManager.Instance.SetShowing(StatsManager.Stat.Memory, value);
            }

            private static void RB3EEnabledCallback(bool value)
            {
                RB3EHardware.Instance.HandleEnabledChanged(value);
            }

            private static void StageKitEnabledCallback(bool value)
            {
                //To avoid being toggled on twice at start
                if (!IsInitialized)
                {
                    return;
                }
                StageKitHardware.Instance.HandleEnabledChanged(value);
            }

            private static void DMXEnabledCallback(bool value)
            {
                SacnHardware.Instance.HandleEnabledChanged(value);
            }

            private static void VSyncCallback(bool value)
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
            }

            private static void FpsCounterCallback(bool value)
            {
#if UNITY_EDITOR
                // Force in editor
                value = true;
#endif

                StatsManager.Instance.SetShowing(StatsManager.Stat.FPS, value);
            }

            private static void FpsCapCallback(int value)
            {
                Application.targetFrameRate = value;
            }

            private static void FullscreenModeCallback(FullScreenMode value)
            {
                // Unity saves this information automatically
                if (!IsInitialized)
                {
                    return;
                }

                Screen.fullScreenMode = value;
            }

            private static void ResolutionCallback(Resolution? value)
            {
                // Unity saves this information automatically
                if (!IsInitialized)
                {
                    return;
                }

                Resolution resolution;

                // If set to null, just get the "default" resolution.
                if (value == null)
                {
                    // Since we actually can't get the highest resolution,
                    // we need to find it in the supported resolutions
                    var highest = new Resolution
                    {
                        width = 0, height = 0, refreshRate = 0
                    };

                    foreach (var r in Screen.resolutions)
                    {
                        if (r.refreshRate >= highest.refreshRate ||
                            r.width >= highest.width ||
                            r.height >= highest.height)
                        {
                            highest = r;
                        }
                    }

                    resolution = highest;
                }
                else
                {
                    resolution = value.Value;
                }

                var fullscreenMode = FullScreenMode.FullScreenWindow;
                if (Settings != null)
                {
                    fullscreenMode = Settings.FullscreenMode.Value;
                }

                Screen.SetResolution(resolution.width, resolution.height, fullscreenMode, resolution.refreshRate);

                // Make sure to refresh the preview since it'll look stretched if we don't
                SettingsMenu.Instance.RefreshPreview(true);
            }

            private static void LowQualityCallback(bool value)
            {
                GraphicsManager.Instance.LowQuality = value;
            }

            private static void DisableBloomCallback(bool value)
            {
                GraphicsManager.Instance.BloomEnabled = !value;
            }

            private static void ShowHitWindowCallback(bool value)
            {
                SettingsMenu.Instance.RefreshPreview();
            }

            private static void VocalMonitoringCallback(float volume)
            {
                foreach (var player in PlayerContainer.Players)
                {
                    player.Bindings.Microphone?.SetMonitoringLevel(volume);
                }
            }

            private static void MusicPlayerVolumeCallback(float volume)
            {
                HelpBar.Instance.MusicPlayer.UpdateVolume(volume);
            }

            // private static void UseWhammyFxChange(bool value)
            // {
            //     AudioManager.UseWhammyFx = value;
            // }

            // private static void WhammyPitchShiftAmountChange(float value)
            // {
            //     AudioManager.WhammyPitchShiftAmount = value;
            // }
            //
            // private static void WhammyOversampleFactorChange(int value)
            // {
            //     AudioManager.WhammyOversampleFactor = value;
            // }

            private static void UseChipmunkSpeedChange(bool value)
            {
                GlobalAudioHandler.IsChipmunkSpeedup = value;
            }

            private static void InputDeviceLoggingCallback(bool value)
            {
                if (!value) return;

                foreach (var device in InputSystem.devices)
                {
                    YargLogger.LogFormatInfo("Description for device {0}:\n{1}\n", device.displayName,
                        item2: device.description.ToJson());
                }
            }
            #endregion
        }
    }
}
