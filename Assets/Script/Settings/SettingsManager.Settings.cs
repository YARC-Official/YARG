using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static partial class SettingsManager {
		private class SettingContainer {
			/*
			
			TODO: THIS IS TERRIBLE. REDO!
			
			*/

			public string[] songFolders = new string[] { };

			[SettingLocation("general", 1)]
			[SettingButton("openSongFolderManager")]
			public void OpenSongFolderManager() {
				MainMenu.Instance.ShowSongFolderManager();
			}

			[SettingLocation("general", 2)]
			[SettingButton("exportOuvertSongs")]
			public void ExportOuvertSongs() {
				StandaloneFileBrowser.SaveFilePanelAsync("Save Song List", null, "songs", "json", path => {
					OuvertExport.ExportOuvertSongsTo(path);
				});
			}

			[SettingLocation("general", 3)]
			[SettingButton("copyCurrentSongTextFilePath")]
			public void CopyCurrentSongTextFilePath() {
				GUIUtility.systemCopyBuffer = TwitchController.Instance.TextFilePath;
			}

			[SettingSpace]
			[SettingLocation("general", 4)]
			[SettingType("Number")]
			public int calibrationNumber = -150;

			[SettingLocation("general", 5)]
			[SettingButton("calibrate")]
			public void Calibrate() {
				if (PlayerManager.players.Count > 0) {
					GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
				}
			}

			[SettingSpace]
			[SettingLocation("general", 6)]
			[SettingType("Toggle")]
			public bool lowQuality = false;

			[SettingChangeFunc("lowQuality")]
			public void LowQualityChange() {
				GraphicsManager.Instance.LowQuality = lowQuality;
			}

			[SettingLocation("general", 7)]
			[SettingType("Toggle")]
			public bool showHitWindow = false;

			[SettingLocation("general", 9)]
			[SettingType("Toggle")]
			public bool muteOnMiss = true;

			[SettingShowInGame]
			[SettingLocation("general", 10)]
			[SettingType("Toggle")]
			public bool useCymbalModelsInFiveLane = true;

			[SettingShowInGame]
			[SettingLocation("general", 11)]
			[SettingType("Toggle")]
			public bool disableBloom = false;

			[SettingChangeFunc("disableBloom")]
			public void DisableBloomChange() {
				GraphicsManager.Instance.BloomEnabled = !disableBloom;
			}

			[SettingSpace]
			[SettingLocation("general", 12)]
			[SettingType("Toggle")]
			public bool noKicks = false;

			[SettingSpace]
			[SettingShowInGame]
			[SettingLocation("general", 13)]
			[SettingType("Toggle")]
			public bool vsync = true;

			[SettingChangeFunc("vsync")]
			public void VsyncChange() {
				QualitySettings.vSyncCount = vsync ? 1 : 0;
			}

			[SettingShowInGame]
			[SettingLocation("general", 14)]
			[SettingType("Number")]
			public int fpsCap = 60;

			[SettingChangeFunc("fpsCap")]
			public void FpsCapChange() {
				Application.targetFrameRate = fpsCap;
			}

			[SettingInteractableFunc("fpsCap")]
			public bool FpsCapInteractable() {
				return QualitySettings.vSyncCount == 0;
			}

			[SettingSpace]
			[SettingShowInGame]
			[SettingLocation("general", 15)]
			[SettingType("Volume")]
			public float masterMusicVolume = 0.9f;

			[SettingChangeFunc("masterMusicVolume")]
			public void SongVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Master, masterMusicVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 16)]
			[SettingType("Volume")]
			public float guitarVolume = 1f;

			[SettingChangeFunc("guitarVolume")]
			public void GuitarVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Guitar, guitarVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 17)]
			[SettingType("Volume")]
			public float rhythmVolume = 1f;

			[SettingChangeFunc("rhythmVolume")]
			public void RhythmVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Rhythm, rhythmVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 18)]
			[SettingType("Volume")]
			public float bassVolume = 1f;

			[SettingChangeFunc("bassVolume")]
			public void BassVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Bass, bassVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 19)]
			[SettingType("Volume")]
			public float keysVolume = 1f;

			[SettingChangeFunc("keysVolume")]
			public void KeysVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Keys, keysVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 20)]
			[SettingType("Volume")]
			public float drumsVolume = 1f;

			[SettingChangeFunc("drumsVolume")]
			public void DrumsVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums, drumsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums1, drumsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums2, drumsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums3, drumsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Drums4, drumsVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 21)]
			[SettingType("Volume")]
			public float vocalsVolume = 1f;

			[SettingChangeFunc("vocalsVolume")]
			public void VocalsVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals, vocalsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals1, vocalsVolume);
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Vocals2, vocalsVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 22)]
			[SettingType("Volume")]
			public float songVolume = 1f;

			[SettingChangeFunc("songVolume")]
			public void MusicVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Song, songVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 23)]
			[SettingType("Volume")]
			public float crowdVolume = 0f;

			[SettingChangeFunc("crowdVolume")]
			public void CrowdVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Crowd, crowdVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 24)]
			[SettingType("Volume")]
			public float sfxVolume = 0.5f;

			[SettingChangeFunc("sfxVolume")]
			public void SfxVolumeChange() {
				GameManager.AudioManager.UpdateVolumeSetting(SongStem.Sfx, sfxVolume);
			}

			[SettingShowInGame]
			[SettingLocation("general", 25)]
			[SettingType("Volume")]
			public float vocalMonitoring = 0.75f;

			[SettingChangeFunc("vocalMonitoring")]
			public void VocalMonitoringChange() {
				AudioManager.Instance.SetVolume("vocalMonitoring", vocalMonitoring);
			}

			[SettingShowInGame]
			[SettingLocation("general", 26)]
			[SettingType("Toggle")]
			public bool useStarpowerFx = true;

			[SettingChangeFunc("useStarpowerFx")]
			public void UseStarpowerFxChange() {
				GameManager.AudioManager.UseStarpowerFx = useStarpowerFx;
			}
			
			[SettingShowInGame]
			[SettingLocation("general", 27)]
			[SettingType("Toggle")]
			public bool useChipmunkSpeed = false;

			[SettingChangeFunc("useChipmunkSpeed")]
			public void UseChipmunkSpeedChange() {
				GameManager.AudioManager.IsChipmunkSpeedup = useChipmunkSpeed;
			}

			[SettingSpace]
			[SettingShowInGame]
			[SettingLocation("general", 28)]
			[SettingType("Toggle")]
			public bool amIAwesome = false;
		}
	}
}
