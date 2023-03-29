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

			[SettingSpace]
			[SettingLocation("general", 11)]
			[SettingType("Toggle")]
			public bool vsync = true;

			[SettingChangeFunc("vsync")]
			public void VsyncChange() {
				QualitySettings.vSyncCount = vsync ? 1 : 0;
			}

			[SettingLocation("general", 12)]
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
			[SettingLocation("general", 13)]
			[SettingType("Text")]
			public string fileServerIp = "localhost";

			[SettingInteractableFunc("fileServerIp")]
			public bool FileServerIpInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 14)]
			[SettingButton("connectToFileServer")]
			public void ConnectToFileServer() {
				GameManager.client = new();
				GameManager.client.Start(fileServerIp);
			}

			[SettingInteractableFunc("connectToFileServer")]
			public bool ConnectToFileServerInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 15)]
			[SettingButton("hostFileServer")]
			public void HostFileServer() {
				GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
			}
		}
	}
}