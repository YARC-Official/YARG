using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Gameplay.HUD;
using YARG.Helpers;
using YARG.Integration;
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
            #region Hidden Settings

            public static bool IsLoading = true;

            public List<string> SongFolders          = new();
            public bool         ShowAntiPiracyDialog = true;

            #endregion

            #region General

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

            public void OpenVenueFolder()
            {
                FileExplorerHelper.OpenFolder(VenueLoader.VenueFolder);
            }

            public ToggleSetting DisablePerSongBackgrounds { get; } = new(false);

            public void OpenCalibrator()
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Calibration);
                SettingsMenu.Instance.gameObject.SetActive(false);
            }

            public IntSetting AudioCalibration { get; } = new(0);
            public IntSetting VideoCalibration { get; } = new(0);

            public ToggleSetting UseCymbalModelsInFiveLane { get; } = new(true);
            public SliderSetting KickBounceMultiplier      { get; } = new(1f, 0f, 2f);

            public SliderSetting ShowCursorTimer { get; } = new(2f, 0f, 5f);

            public ToggleSetting AmIAwesome { get; } = new(false);

            public ToggleSetting InputDeviceLogging              { get; } = new(false, InputDeviceLoggingCallback);
            public ToggleSetting ShowAdvancedMusicLibraryOptions { get; } = new(false);

            #endregion

            #region Sound

            public VolumeSetting MasterMusicVolume { get; } = new(0.75f, v => VolumeCallback(SongStem.Master, v));
            public VolumeSetting GuitarVolume      { get; } = new(1f, v => VolumeCallback(SongStem.Guitar, v));
            public VolumeSetting RhythmVolume      { get; } = new(1f, v => VolumeCallback(SongStem.Rhythm, v));
            public VolumeSetting BassVolume        { get; } = new(1f, v => VolumeCallback(SongStem.Bass, v));
            public VolumeSetting KeysVolume        { get; } = new(1f, v => VolumeCallback(SongStem.Keys, v));
            public VolumeSetting DrumsVolume       { get; } = new(1f, DrumVolumeCallback);
            public VolumeSetting VocalsVolume      { get; } = new(1f, VocalVolumeCallback);
            public VolumeSetting SongVolume        { get; } = new(1f, v => VolumeCallback(SongStem.Song, v));
            public VolumeSetting CrowdVolume       { get; } = new(0.5f, v => VolumeCallback(SongStem.Crowd, v));
            public VolumeSetting SfxVolume         { get; } = new(0.8f, v => VolumeCallback(SongStem.Sfx, v));
            public VolumeSetting PreviewVolume     { get; } = new(0.25f);
            public VolumeSetting MusicPlayerVolume { get; } = new(0.15f, MusicPlayerVolumeCallback);
            public VolumeSetting VocalMonitoring   { get; } = new(0.7f, VocalMonitoringCallback);

            public SliderSetting MicrophoneSensitivity { get; } = new(2f, -50f, 50f);
            public ToggleSetting MuteOnMiss            { get; } = new(true);

            public ToggleSetting UseStarpowerFx { get; } = new(true, UseStarpowerFxChange);

            // public ToggleSetting UseWhammyFx            { get; } = new(true, UseWhammyFxChange);
            // public SliderSetting WhammyPitchShiftAmount { get; } = new(1, 1, 12, WhammyPitchShiftAmountChange);
            // public IntSetting    WhammyOversampleFactor { get; } = new(8, 4, 32, WhammyOversampleFactorChange);
            public ToggleSetting UseChipmunkSpeed { get; } = new(false, UseChipmunkSpeedChange);

            #endregion

            #region Graphics

            public ToggleSetting VSync  { get; } = new(true, VSyncCallback);
            public IntSetting    FpsCap { get; } = new(60, 1, onChange: FpsCapCallback);

            public DropdownSetting<FullScreenMode> FullscreenMode { get; } = new(new()
            {
#if UNITY_STANDALONE_WIN
                FullScreenMode.ExclusiveFullScreen,
#elif UNITY_STANDALONE_OSX
                FullScreenMode.MaximizedWindow,
#endif
                FullScreenMode.FullScreenWindow,
                FullScreenMode.Windowed,
            }, FullScreenMode.FullScreenWindow, FullscreenModeCallback);

            public ResolutionSetting Resolution { get; } = new(ResolutionCallback);
            public ToggleSetting     FpsStats   { get; } = new(false, FpsCounterCallback);

            public ToggleSetting LowQuality   { get; } = new(false, LowQualityCallback);
            public ToggleSetting DisableBloom { get; } = new(false, DisableBloomCallback);

            public ToggleSetting ShowHitWindow            { get; } = new(false);
            public ToggleSetting DisableTextNotifications { get; } = new(false);

            public DropdownSetting<SongProgressMode> SongTimeOnScoreBox { get; } = new(new()
            {
                SongProgressMode.None,
                SongProgressMode.CountUpAndTotal,
                SongProgressMode.CountDownAndTotal,
                SongProgressMode.CountUpOnly,
                SongProgressMode.CountDownOnly,
                SongProgressMode.TotalOnly
            }, SongProgressMode.CountUpOnly);

            public ToggleSetting GraphicalProgressOnScoreBox { get; } = new(true);

            public DropdownSetting<LyricDisplayMode> LyricDisplay { get; } = new(new()
            {
                LyricDisplayMode.Normal,
                LyricDisplayMode.Transparent,
                LyricDisplayMode.NoBackground,
                LyricDisplayMode.Disabled
            }, LyricDisplayMode.Normal);

            #endregion

            #region Callbacks

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
                if (IsLoading)
                {
                    return;
                }

                Screen.fullScreenMode = value;
            }

            private static void ResolutionCallback(Resolution? value)
            {
                // Unity saves this information automatically
                if (IsLoading)
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
            }

            private static void LowQualityCallback(bool value)
            {
                GraphicsManager.Instance.LowQuality = value;
            }

            private static void DisableBloomCallback(bool value)
            {
                GraphicsManager.Instance.BloomEnabled = !value;
            }

            private static void VolumeCallback(SongStem stem, float volume)
            {
                GlobalVariables.AudioManager.UpdateVolumeSetting(stem, volume);
            }

            private static void DrumVolumeCallback(float volume)
            {
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Drums, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Drums1, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Drums2, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Drums3, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Drums4, volume);
            }

            private static void VocalVolumeCallback(float volume)
            {
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Vocals, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Vocals1, volume);
                GlobalVariables.AudioManager.UpdateVolumeSetting(SongStem.Vocals2, volume);
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
                HelpBar.Instance.MusicPlayer.UpdateVolume();
            }

            private static void UseStarpowerFxChange(bool value)
            {
                GlobalVariables.AudioManager.Options.UseStarpowerFx = value;
            }

            // private static void UseWhammyFxChange(bool value)
            // {
            //     GameManager.AudioManager.Options.UseWhammyFx = value;
            // }

            private static void WhammyPitchShiftAmountChange(float value)
            {
                GlobalVariables.AudioManager.Options.WhammyPitchShiftAmount = value;
            }

            private static void WhammyOversampleFactorChange(int value)
            {
                GlobalVariables.AudioManager.Options.WhammyOversampleFactor = value;
            }

            private static void UseChipmunkSpeedChange(bool value)
            {
                GlobalVariables.AudioManager.Options.IsChipmunkSpeedup = value;
            }

            private static void InputDeviceLoggingCallback(bool value)
            {
                if (!value) return;

                foreach (var device in InputSystem.devices)
                {
                    Debug.Log($"Description for device {device.displayName}:\n{device.description.ToJson()}\n");
                }
            }

            #endregion
        }
    }
}