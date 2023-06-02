using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Audio;
using YARG.PlayMode;
using YARG.Serialization;
using YARG.Settings.Types;
using YARG.UI;
using YARG.Util;
using YARG.Venue;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class SettingContainer {
			public static bool IsLoading = true;

#pragma warning disable format

			public List<string>  SongFolders                                      = new();

			public IntSetting    AudioCalibration           { get; private set; } = new(120);

			public ToggleSetting DisablePerSongBackgrounds  { get; private set; } = new(false);

			public ToggleSetting     VSync                  { get; private set; } = new(true,      VSyncCallback);
			public IntSetting        FpsCap                 { get; private set; } = new(60, 1,     onChange: FpsCapCallback);
			public EnumSetting       FullscreenMode         { get; private set; } = new(typeof(FullScreenMode),
				                                            (int) FullScreenMode.FullScreenWindow, FullscreenModeCallback);
			public ResolutionSetting Resolution             { get; private set; } = new(           ResolutionCallback);
			public ToggleSetting     FpsStats               { get; private set; } = new(false,     FpsCounterCallback);

			public ToggleSetting LowQuality                 { get; private set; } = new(false,     LowQualityCallback);
			public ToggleSetting DisableBloom               { get; private set; } = new(false,     DisableBloomCallback);

			public ToggleSetting ShowHitWindow              { get; private set; } = new(false);
			public ToggleSetting UseCymbalModelsInFiveLane  { get; private set; } = new(true);

			public ToggleSetting NoKicks                    { get; private set; } = new(false);
			public ToggleSetting AntiGhosting               { get; private set; } = new(true);

			public VolumeSetting MasterMusicVolume          { get; private set; } = new(0.75f,v => VolumeCallback(SongStem.Master, v));
			public VolumeSetting GuitarVolume               { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Guitar, v));
			public VolumeSetting RhythmVolume               { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Rhythm, v));
			public VolumeSetting BassVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Bass,   v));
			public VolumeSetting KeysVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Keys,   v));
			public VolumeSetting DrumsVolume                { get; private set; } = new(1f,        DrumVolumeCallback);
			public VolumeSetting VocalsVolume               { get; private set; } = new(1f,        VocalVolumeCallback);
			public VolumeSetting SongVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Song,   v));
			public VolumeSetting CrowdVolume                { get; private set; } = new(0.5f, v => VolumeCallback(SongStem.Crowd,  v));
			public VolumeSetting SfxVolume                  { get; private set; } = new(0.8f, v => VolumeCallback(SongStem.Sfx,    v));
			public VolumeSetting PreviewVolume              { get; private set; } = new(0.25f);
			public VolumeSetting MusicPlayerVolume          { get; private set; } = new(0.15f,     MusicPlayerVolumeCallback);
			public VolumeSetting VocalMonitoring            { get; private set; } = new(0.7f,      VocalMonitoringCallback);

			public SliderSetting MicrophoneSensitivity      { get; private set; } = new(2f,    -50f, 50f);

			public ToggleSetting MuteOnMiss                 { get; private set; } = new(true);
			public ToggleSetting UseStarpowerFx             { get; private set; } = new(true,      UseStarpowerFxChange);
			public ToggleSetting UseChipmunkSpeed           { get; private set; } = new(false,     UseChipmunkSpeedChange);

			public SliderSetting TrackCamFOV                { get; private set; } = new(55f,    40f, 150f, CameraPosChange);
			public SliderSetting TrackCamYPos               { get; private set; } = new(2.66f,  0f,  4f,   CameraPosChange);
			public SliderSetting TrackCamZPos               { get; private set; } = new(1.14f,  0f,  12f,  CameraPosChange);
			public SliderSetting TrackCamRot                { get; private set; } = new(24.12f, 0f,  180f, CameraPosChange);

			public ToggleSetting DisableTextNotifications   { get; private set; } = new(false);

			public ToggleSetting AmIAwesome                 { get; private set; } = new(false);

#pragma warning restore format

			public void OpenSongFolderManager() {
				SettingsMenu.Instance.CurrentTab = "_SongFolderManager";
			}

			public void OpenVenueFolder() {
				FileExplorerHelper.OpenFolder(VenueLoader.VenueFolder);
			}

			public void ExportOuvertSongs() {
				FileExplorerHelper.OpenSaveFile(null, "songs", "json", OuvertExport.ExportOuvertSongsTo);
			}

			public void CopyCurrentSongTextFilePath() {
				GUIUtility.systemCopyBuffer = TwitchController.Instance.TextFilePath;
			}

			public void CopyCurrentSongJsonFilePath() {
				GUIUtility.systemCopyBuffer = TwitchController.Instance.JsonFilePath;
			}

			public void OpenCalibrator() {
				GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
				SettingsMenu.Instance.gameObject.SetActive(false);
			}

			private static void VSyncCallback(bool value) {
				QualitySettings.vSyncCount = value ? 1 : 0;
			}

			private static void FpsCounterCallback(bool value) {
				// disable script
				FpsCounter.Instance.enabled = value;
				FpsCounter.Instance.SetVisible(value);

				// enable script if in editor
#if UNITY_EDITOR
				FpsCounter.Instance.enabled = true;
				FpsCounter.Instance.SetVisible(true);
#endif
			}

			private static void FpsCapCallback(int value) {
				Application.targetFrameRate = value;
			}

			private static void FullscreenModeCallback(int value) {
				// Unity saves this information automatically
				if (IsLoading) {
					return;
				}

				Screen.fullScreenMode = (FullScreenMode) value;
			}

			private static void ResolutionCallback(Resolution? value) {
				// Unity saves this information automatically
				if (IsLoading) {
					return;
				}

				Resolution resolution;

				// If set to null, just get the "default" resolution.
				if (value == null) {
					// Since we actually can't get the highest resolution,
					// we need to find it in the supported resolutions
					var highest = new Resolution {
						width = 0,
						height = 0,
						refreshRate = 0
					};

					foreach (var r in Screen.resolutions) {
						if (r.refreshRate >= highest.refreshRate &&
						    r.width >= highest.width &&
						    r.height >= highest.height) {

							highest = r;
						}
					}

					resolution = highest;
				} else {
					resolution = value.Value;
				}

				var fullscreenMode = FullScreenMode.FullScreenWindow;
				if (Settings != null) {
					fullscreenMode = (FullScreenMode) Settings.FullscreenMode.Data;
				}

				Screen.SetResolution(resolution.width, resolution.height, fullscreenMode, resolution.refreshRate);
			}

			private static void LowQualityCallback(bool value) {
				GraphicsManager.Instance.LowQuality = value;
				CameraPositioner.UpdateAllAntiAliasing();
			}

			private static void DisableBloomCallback(bool value) {
				GraphicsManager.Instance.BloomEnabled = !value;
			}

			private static void VolumeCallback(SongStem stem, float volume) {
				GameManager.AudioManager.UpdateVolumeSetting(stem, volume);
			}

			private static void DrumVolumeCallback(float volume) {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums1, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums2, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums3, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums4, volume);
			}

			private static void VocalVolumeCallback(float volume) {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals1, volume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals2, volume);
			}

			private static void VocalMonitoringCallback(float volume) {
				foreach (var player in PlayerManager.players) {
					player.inputStrategy?.MicDevice?.SetMonitoringLevel(volume);
				}
			}

			private static void MusicPlayerVolumeCallback(float volume) {
				HelpBar.Instance.MusicPlayer.UpdateVolume();
			}

			private static void UseStarpowerFxChange(bool value) {
				GameManager.AudioManager.UseStarpowerFx = value;
			}

			private static void UseChipmunkSpeedChange(bool value) {
				GameManager.AudioManager.IsChipmunkSpeedup = value;
			}

			private static void CameraPosChange(float value) {
				CameraPositioner.UpdateAllPosition();
			}
		}
	}
}
