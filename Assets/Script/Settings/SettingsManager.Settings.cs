using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Audio;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Integration;
using YARG.Menu.Settings;
using YARG.Settings.Types;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Song;
using YARG.Venue;
using UnityEngine.InputSystem;
using YARG.Core.Audio;

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        public class SettingContainer
        {
            #region Hidden Settings

            public static bool IsLoading = true;

            public List<string> SongFolders = new();
            public bool ShowAntiPiracyDialog = true;

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

            public IntSetting    AudioCalibration          { get; } = new(120);
            public IntSetting    VideoCalibration          { get; } = new(0);

            public ToggleSetting UseCymbalModelsInFiveLane { get; } = new(true);
            public ToggleSetting KickBounce                { get; } = new(true);

            public SliderSetting ShowCursorTimer           { get; } = new(2f, 0f, 5f);

            public ToggleSetting AmIAwesome                { get; } = new(false);

            public ToggleSetting InputDeviceLogging        { get; } = new(false, InputDeviceLoggingCallback);

            #endregion

            #region Sound

            public VolumeSetting MasterMusicVolume { get; } = new(0.75f, v => VolumeCallback(SongStem.Master, v));
            public VolumeSetting GuitarVolume      { get; } = new(1f,    v => VolumeCallback(SongStem.Guitar, v));
            public VolumeSetting RhythmVolume      { get; } = new(1f,    v => VolumeCallback(SongStem.Rhythm, v));
            public VolumeSetting BassVolume        { get; } = new(1f,    v => VolumeCallback(SongStem.Bass, v));
            public VolumeSetting KeysVolume        { get; } = new(1f,    v => VolumeCallback(SongStem.Keys, v));
            public VolumeSetting DrumsVolume       { get; } = new(1f,    DrumVolumeCallback);
            public VolumeSetting VocalsVolume      { get; } = new(1f,    VocalVolumeCallback);
            public VolumeSetting SongVolume        { get; } = new(1f,    v => VolumeCallback(SongStem.Song, v));
            public VolumeSetting CrowdVolume       { get; } = new(0.5f,  v => VolumeCallback(SongStem.Crowd, v));
            public VolumeSetting SfxVolume         { get; } = new(0.8f,  v => VolumeCallback(SongStem.Sfx, v));
            public VolumeSetting PreviewVolume     { get; } = new(0.25f);
            public VolumeSetting MusicPlayerVolume { get; } = new(0.15f, MusicPlayerVolumeCallback);
            public VolumeSetting VocalMonitoring   { get; } = new(0.7f,  VocalMonitoringCallback);

            public SliderSetting MicrophoneSensitivity  { get; } = new(2f, -50f, 50f);
            public ToggleSetting MuteOnMiss             { get; } = new(true);
            public ToggleSetting UseStarpowerFx         { get; } = new(true, UseStarpowerFxChange);
         // public ToggleSetting UseWhammyFx            { get; } = new(true, UseWhammyFxChange);
         // public SliderSetting WhammyPitchShiftAmount { get; } = new(1, 1, 12, WhammyPitchShiftAmountChange);
         // public IntSetting    WhammyOversampleFactor { get; } = new(8, 4, 32, WhammyOversampleFactorChange);
            public ToggleSetting UseChipmunkSpeed       { get; } = new(false, UseChipmunkSpeedChange);

            #endregion

            #region Graphics

            public ToggleSetting VSync   { get; } = new(true, VSyncCallback);
            public IntSetting    FpsCap  { get; } = new(60, 1, onChange: FpsCapCallback);

            public DropdownSetting FullscreenMode { get; } = new(new()
            {
#if UNITY_STANDALONE_WIN
                "ExclusiveFullScreen",
#elif UNITY_STANDALONE_OSX
                "MaximizedWindow",
#endif
                "FullScreenWindow",
                "Windowed",
            }, "FullScreenWindow", FullscreenModeCallback);

            public ResolutionSetting Resolution { get; } = new(ResolutionCallback);
            public ToggleSetting     FpsStats   { get; } = new(false, FpsCounterCallback);

            public ToggleSetting LowQuality   { get; } = new(false, LowQualityCallback);
            public ToggleSetting DisableBloom { get; } = new(false, DisableBloomCallback);

            public ToggleSetting ShowHitWindow            { get; } = new(false);
            public ToggleSetting DisableTextNotifications { get; } = new(false);

            public DropdownSetting SongTimeOnScoreBox { get; } = new(new()
            {
                "None",
                "CountUpAndTotal", "CountDownAndTotal",
                "CountUpOnly", "CountDownOnly", "TotalOnly"
            }, "CountUp");

            public ToggleSetting GraphicalProgressOnScoreBox { get; } = new(true);

            public DropdownSetting LyricDisplay { get; } = new(new()
            {
                "Normal", "Transparent", "NoBackground",
                "NoLyricDisplay"
            }, "Normal");

            #endregion

            #region Engine

            public ToggleSetting NoKicks          { get; } = new(false);
            public ToggleSetting AntiGhosting     { get; } = new(true);
            public ToggleSetting InfiniteFrontEnd { get; } = new(false);

            #endregion

            #region Preset Fields

            // This is kind of a hack for preset fields. All of these values are not saved in the settings.json,
            // and are solely used by the "Presets" tab. This makes it 10x easier to bind setting visuals without
            // the need of overcomplicating it. Sure, this is kinda hacky, but it works just fine.

            // All names should be: <PresetClass>_<PresetField>
            // ReSharper disable InconsistentNaming

            [JsonIgnore]
            public SliderSetting CameraPreset_FieldOfView  { get; } = new(55f, 40f, 150f);
            [JsonIgnore]
            public SliderSetting CameraPreset_PositionY    { get; } = new(2.66f, 0f, 4f);
            [JsonIgnore]
            public SliderSetting CameraPreset_PositionZ    { get; } = new(1.14f, 0f, 12f);
            [JsonIgnore]
            public SliderSetting CameraPreset_Rotation     { get; } = new(24.12f, 0f, 180f);
            [JsonIgnore]
            public SliderSetting CameraPreset_FadeLength   { get; } = new(1.75f, 0f, 5f);
            [JsonIgnore]
            public SliderSetting CameraPreset_CurveFactor  { get; } = new(0.5f, -3f, 3f);

            [JsonIgnore]
            public ColorProfile ColorProfile_Ref = ColorProfile.Default;

            // ReSharper restore InconsistentNaming

            #endregion

            #region Callbacks

            private static void VSyncCallback(bool value)
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
            }

            private static void FpsCounterCallback(bool value)
            {
                // disable script
                FpsCounter.Instance.enabled = value;
                FpsCounter.Instance.SetVisible(value);

                // enable script if in editor
#if UNITY_EDITOR
                FpsCounter.Instance.enabled = true;
                FpsCounter.Instance.SetVisible(true);
#endif
            }

            private static void FpsCapCallback(int value)
            {
                Application.targetFrameRate = value;
            }

            private static void FullscreenModeCallback(string value)
            {
                // Unity saves this information automatically
                if (IsLoading)
                {
                    return;
                }

                Screen.fullScreenMode = Enum.Parse<FullScreenMode>(value);
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
                    fullscreenMode = Enum.Parse<FullScreenMode>(Settings.FullscreenMode.Data);
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
                if (!value)
                    return;

                foreach (var device in InputSystem.devices)
                {
                    Debug.Log($"Description for device {device.displayName}:\n{device.description.ToJson()}\n");
                }
            }

            #endregion
        }
    }
}