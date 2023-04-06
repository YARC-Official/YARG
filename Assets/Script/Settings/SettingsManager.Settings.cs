using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static partial class SettingsManager {
		private class SettingContainer {
			[SettingLocation("general", 1)]
			[SettingType("Folder")]
			public string songFolder = null;

			[SettingInteractableFunc("songFolder")]
			public bool SongFolderInteractable() {
				return GameManager.client == null;
			}

			[SettingChangeFunc("songFolder")]
			public void SongFolderChange() {
				if (MainMenu.Instance != null) {
					MainMenu.Instance.RefreshSongLibrary();
				}
			}

			[SettingLocation("general", 2)]
			[SettingButton("refreshCache")]
			public void RefreshCache() {
				MainMenu.Instance.RefreshCache();
			}

			[SettingInteractableFunc("refreshCache")]
			public bool RefreshCacheInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 3)]
			[SettingButton("exportOuvertSongs")]
			public void ExportOuvertSongs() {
				StandaloneFileBrowser.SaveFilePanelAsync("Save Song List", null, "songs", "json", path => {
					OuvertExport.ExportOuvertSongsTo(path);
				});
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
				QualitySettings.SetQualityLevel(lowQuality ? 0 : 1, true);
			}

			[SettingLocation("general", 7)]
			[SettingType("Toggle")]
			public bool showHitWindow = false;

			[SettingLocation("general", 8)]
			[SettingType("Toggle")]
			public bool useAudioTime = false;

			[SettingLocation("general", 9)]
			[SettingType("Toggle")]
			public bool muteOnMiss = true;

			[SettingLocation("general", 10)]
			[SettingType("Toggle")]
			public bool useCymbalModelsInFiveLane = true;

			[SettingLocation("general", 11)]
			[SettingType("Toggle")]
			public bool noKicks = false;

			[SettingSpace]
			[SettingLocation("general", 12)]
			[SettingType("Toggle")]
			public bool vsync = true;

			[SettingChangeFunc("vsync")]
			public void VsyncChange() {
				QualitySettings.vSyncCount = vsync ? 1 : 0;
			}

			[SettingLocation("general", 13)]
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
			[SettingLocation("general", 14)]
			[SettingType("Volume")]
			public float musicVolume = 0.9f;

			[SettingChangeFunc("musicVolume")]
			public void SongVolumeChange() {
				AudioManager.Instance.SetVolume("music", musicVolume);
			}

			[SettingLocation("general", 15)]
			[SettingType("Volume")]
			public float guitarVolume = 1f;

			[SettingChangeFunc("guitarVolume")]
			public void GuitarVolumeChange() {
				AudioManager.Instance.SetVolume("guitar", guitarVolume);
			}

			[SettingLocation("general", 16)]
			[SettingType("Volume")]
			public float bassVolume = 1f;

			[SettingChangeFunc("bassVolume")]
			public void BassVolumeChange() {
				AudioManager.Instance.SetVolume("bass", bassVolume);
			}

			[SettingLocation("general", 17)]
			[SettingType("Volume")]
			public float keysVolume = 1f;

			[SettingChangeFunc("keysVolume")]
			public void KeysVolumeChange() {
				AudioManager.Instance.SetVolume("keys", keysVolume);
			}

			[SettingLocation("general", 18)]
			[SettingType("Volume")]
			public float drumsVolume = 1f;

			[SettingChangeFunc("drumsVolume")]
			public void DrumsVolumeChange() {
				AudioManager.Instance.SetVolume("drums", drumsVolume);
			}

			[SettingLocation("general", 19)]
			[SettingType("Volume")]
			public float vocalsVolume = 1f;

			[SettingChangeFunc("vocalsVolume")]
			public void VocalsVolumeChange() {
				AudioManager.Instance.SetVolume("vocals", vocalsVolume);
			}

			[SettingLocation("general", 20)]
			[SettingType("Volume")]
			public float songVolume = 1f;

			[SettingChangeFunc("songVolume")]
			public void MusicVolumeChange() {
				AudioManager.Instance.SetVolume("song", songVolume);
			}

			[SettingLocation("general", 21)]
			[SettingType("Volume")]
			public float crowdVolume = 0f;

			[SettingChangeFunc("crowdVolume")]
			public void CrowdVolumeChange() {
				AudioManager.Instance.SetVolume("crowd", crowdVolume);
			}

			[SettingLocation("general", 22)]
			[SettingType("Volume")]
			public float vocalMonitoring = 0.75f;

			[SettingChangeFunc("vocalMonitoring")]
			public void VocalMonitoringChange() {
				AudioManager.Instance.SetVolume("vocalMonitoring", vocalMonitoring);
			}

			[SettingSpace]
			[SettingLocation("general", 23)]
			[SettingType("Text")]
			public string fileServerIp = "localhost";

			[SettingInteractableFunc("fileServerIp")]
			public bool FileServerIpInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 24)]
			[SettingButton("connectToFileServer")]
			public void ConnectToFileServer() {
				GameManager.client = new();
				GameManager.client.Start(fileServerIp);
			}

			[SettingInteractableFunc("connectToFileServer")]
			public bool ConnectToFileServerInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 25)]
			[SettingButton("hostFileServer")]
			public void HostFileServer() {
				GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
			}
		}
	}
}