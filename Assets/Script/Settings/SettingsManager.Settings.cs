using System;
using System.Collections.Generic;
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

			public IntSetting    AudioCalibration           { get; } = new(120);

			public ToggleSetting DisablePerSongBackgrounds  { get; } = new(false);

			public SliderSetting PressThreshold             { get; } = new(0.75f, 0f, 1f);
			public SliderSetting ShowCursorTimer            { get; } = new(2f, 0f, 5f);

			public ToggleSetting     VSync                  { get; } = new(true,      VSyncCallback);
			public IntSetting        FpsCap                 { get; } = new(60, 1,     onChange: FpsCapCallback);
			public DropdownSetting   FullscreenMode         { get; } = new(new() {
#if UNITY_STANDALONE_WIN
					"ExclusiveFullScreen",
#elif UNITY_STANDALONE_OSX
					"MaximizedWindow",
#endif
					"FullScreenWindow",
					"Windowed",
				}, "FullScreenWindow", FullscreenModeCallback);
			public ResolutionSetting Resolution             { get; } = new(           ResolutionCallback);
			public ToggleSetting     FpsStats               { get; } = new(false,     FpsCounterCallback);

			public ToggleSetting LowQuality                 { get; } = new(false,     LowQualityCallback);
			public ToggleSetting DisableBloom               { get; } = new(false,     DisableBloomCallback);

			public ToggleSetting ShowHitWindow              { get; } = new(false);
			public ToggleSetting UseCymbalModelsInFiveLane  { get; } = new(true);

			public ToggleSetting NoKicks                    { get; } = new(false);
			public ToggleSetting KickBounce                 { get; } = new(true);
			public ToggleSetting AntiGhosting               { get; } = new(true);

			public VolumeSetting MasterMusicVolume          { get; } = new(0.75f,v => VolumeCallback(SongStem.Master, v));
			public VolumeSetting GuitarVolume               { get; } = new(1f,   v => VolumeCallback(SongStem.Guitar, v));
			public VolumeSetting RhythmVolume               { get; } = new(1f,   v => VolumeCallback(SongStem.Rhythm, v));
			public VolumeSetting BassVolume                 { get; } = new(1f,   v => VolumeCallback(SongStem.Bass,   v));
			public VolumeSetting KeysVolume                 { get; } = new(1f,   v => VolumeCallback(SongStem.Keys,   v));
			public VolumeSetting DrumsVolume                { get; } = new(1f,        DrumVolumeCallback);
			public VolumeSetting VocalsVolume               { get; } = new(1f,        VocalVolumeCallback);
			public VolumeSetting SongVolume                 { get; } = new(1f,   v => VolumeCallback(SongStem.Song,   v));
			public VolumeSetting CrowdVolume                { get; } = new(0.5f, v => VolumeCallback(SongStem.Crowd,  v));
			public VolumeSetting SfxVolume                  { get; } = new(0.8f, v => VolumeCallback(SongStem.Sfx,    v));
			public VolumeSetting PreviewVolume              { get; } = new(0.25f);
			public VolumeSetting MusicPlayerVolume          { get; } = new(0.15f,     MusicPlayerVolumeCallback);
			public VolumeSetting VocalMonitoring            { get; } = new(0.7f,      VocalMonitoringCallback);

			public SliderSetting MicrophoneSensitivity      { get; } = new(2f,    -50f, 50f);

			public ToggleSetting MuteOnMiss                 { get; } = new(true);
			public ToggleSetting UseStarpowerFx             { get; } = new(true,      UseStarpowerFxChange);
			public ToggleSetting UseChipmunkSpeed           { get; } = new(false,     UseChipmunkSpeedChange);

			public SliderSetting TrackCamFOV                { get; } = new(55f,    40f, 150f,    CameraPosChange);
			public SliderSetting TrackCamYPos               { get; } = new(2.66f,  0f,  4f,      CameraPosChange);
			public SliderSetting TrackCamZPos               { get; } = new(1.14f,  0f,  12f,     CameraPosChange);
			public SliderSetting TrackCamRot                { get; } = new(24.12f, 0f,  180f,    CameraPosChange);
			public SliderSetting TrackFadePosition          { get; } = new(3f,     0f,  3f, v => FadeChange(true,  v));
			public SliderSetting TrackFadeSize              { get; } = new(1.75f,  0f,  5f, v => FadeChange(false, v));

			public ToggleSetting   DisableTextNotifications { get; } = new(false);
			public DropdownSetting LyricBackground          { get; } = new(new() {
				"Normal",
				"Transparent",
				"None",
			}, "Normal");

			public ToggleSetting AmIAwesome                 { get; } = new(false);

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

			private static void FullscreenModeCallback(string value) {
				// Unity saves this information automatically
				if (IsLoading) {
					return;
				}

				Screen.fullScreenMode = Enum.Parse<FullScreenMode>(value);
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
						if (r.refreshRate >= highest.refreshRate ||
							r.width >= highest.width ||
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
					fullscreenMode = Enum.Parse<FullScreenMode>(Settings.FullscreenMode.Data);
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

			private static void FadeChange(bool isPosition, float value) {
				if (IsLoading) {
					return;
				}

				float position;
				float size;

				if (isPosition) {
					position = value;
					size = Settings.TrackFadeSize.Data;
				} else {
					position = Settings.TrackFadePosition.Data;
					size = value;
				}

				// Yes, it's inefficient, but it only gets updated when the setting does.

				// ReSharper disable Unity.PreferAddressByIdToGraphicsParams
				Shader.SetGlobalVector("_FadeZeroPosition", new Vector4(0f, 0f, position, 0f));
				Shader.SetGlobalVector("_FadeFullPosition", new Vector4(0f, 0f, position - size, 0f));
				// ReSharper restore Unity.PreferAddressByIdToGraphicsParams
			}
		}
	}
}
