using UnityEngine;
using YARG.Settings.SettingTypes;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class SettingContainer {
#pragma warning disable format
			
			public string[]      SongFolders                                      = { };
			
			public IntSetting    CalibrationNumber          { get; private set; } = new(-120);
			
			public ToggleSetting VSync                      { get; private set; } = new(true, VSyncCallback);
			public IntSetting    FpsCap                     { get; private set; } = new(60, 1, onChange: FpsCapCallback);
			
			public ToggleSetting LowQuality                 { get; private set; } = new(false, LowQualityCallback);
			public ToggleSetting DisableBloom               { get; private set; } = new(false, DisableBloomCallback);
			public ToggleSetting HighFovCamera              { get; private set; } = new(false);
			
			public ToggleSetting ShowHitWindow              { get; private set; } = new(false);
			public ToggleSetting UseCymbalModelsInFiveLane  { get; private set; } = new(true);
			
			public ToggleSetting NoKicks                    { get; private set; }
			public ToggleSetting AntiGhosting               { get; private set; } = new(true);
			
			public VolumeSetting MasterMusicVolume          { get; private set; } = new(0.9f, v => VolumeCallback(SongStem.Master, v));
			public VolumeSetting GuitarVolume               { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Guitar, v));
			public VolumeSetting RhythmVolume               { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Rhythm, v));
			public VolumeSetting BassVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Bass,   v));
			public VolumeSetting KeysVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Keys,   v));
			public VolumeSetting DrumsVolume                { get; private set; } = new(1f,   DrumVolumeCallback);
			public VolumeSetting VocalsVolume               { get; private set; } = new(1f,   VocalVolumeCallback);
			public VolumeSetting SongVolume                 { get; private set; } = new(1f,   v => VolumeCallback(SongStem.Song,   v));
			public VolumeSetting CrowdVolume                { get; private set; } = new(0f,   v => VolumeCallback(SongStem.Crowd,  v));
			public VolumeSetting SfxVolume                  { get; private set; } = new(0.7f, v => VolumeCallback(SongStem.Sfx,    v));
			public VolumeSetting VocalMonitoring            { get; private set; } = new(0.7f, VocalMonitoringCallback);
			public ToggleSetting MuteOnMiss                 { get; private set; } = new(true);
			public ToggleSetting UserStarpowerFx            { get; private set; } = new(true, UseStarpowerFxChange);
			// public ToggleSetting ClapsInStarpower        { get; private set; } = new(true);
			// public ToggleSetting ReverbInStarpower       { get; private set; } = new(true);
			public ToggleSetting UseChipmunkSpeed           { get; private set; } = new(false, UseChipmunkSpeedChange);
			
			public ToggleSetting AmIAwesome                 { get; private set; } = new(false);

#pragma warning restore format
		
			private static void VSyncCallback(bool value) {
				QualitySettings.vSyncCount = value ? 1 : 0;
			}

			private static void FpsCapCallback(int value) {
				Application.targetFrameRate = value;
			}

			private static void LowQualityCallback(bool value) {
				GraphicsManager.Instance.LowQuality = value;
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
				AudioManager.Instance.SetVolume("vocalMonitoring", volume);
			}

			private static void UseStarpowerFxChange(bool value) {
				GameManager.AudioManager.UseStarpowerFx = value;
			}

			private static void UseChipmunkSpeedChange(bool value) {
				GameManager.AudioManager.IsChipmunkSpeedup = value;
			}
		}
	}
}
