using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static partial class SettingsManager {
		private class SettingContainer {
			[SettingLocation("general", 10)]
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

			[SettingLocation("general", 20)]
			[SettingButton("refreshCache")]
			public void RefreshCache() {
				MainMenu.Instance.RefreshCache();
			}

			[SettingInteractableFunc("refreshCache")]
			public bool RefreshCacheInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 30)]
			[SettingButton("exportOuvertSongs")]
			public void ExportOuvertSongs() {
				StandaloneFileBrowser.SaveFilePanelAsync("Save Song List", null, "songs", "json", path => {
					OuvertExport.ExportOuvertSongsTo(path);
				});
			}

			[SettingSpace]
			[SettingLocation("general", 40)]
			[SettingType("Number")]
			public int calibrationNumber = (int) (PlayerManager.globalCalibration * 1000f);

			[SettingChangeFunc("calibrationNumber")]
			public void CalibrationNumberChange() {
				PlayerManager.globalCalibration = calibrationNumber / 1000f;
			}

			[SettingLocation("general", 50)]
			[SettingButton("calibrate")]
			public void Calibrate() {
				if (PlayerManager.players.Count > 0) {
					GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
				}
			}

			[SettingSpace]
			[SettingLocation("general", 60)]
			[SettingType("Toggle")]
			public bool lowQuality = false;

			[SettingChangeFunc("lowQuality")]
			public void LowQualityChange() {
				QualitySettings.SetQualityLevel(lowQuality ? 0 : 1, true);
			}

			[SettingLocation("general", 70)]
			[SettingType("Toggle")]
			public bool showHitWindow = false;

			[SettingLocation("general", 80)]
			[SettingType("Toggle")]
			public bool useAudioTime = false;

			[SettingLocation("general", 90)]
			[SettingType("Toggle")]
			public bool vsync = true;

			[SettingChangeFunc("vsync")]
			public void VsyncChange() {
				QualitySettings.vSyncCount = vsync ? 1 : 0;
			}

			[SettingSpace]
			[SettingLocation("general", 100)]
			[SettingType("Text")]
			public string fileServerIp = "localhost";

			[SettingInteractableFunc("fileServerIp")]
			public bool FileServerIpInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 110)]
			[SettingButton("connectToFileServer")]
			public void ConnectToFileServer() {
				GameManager.client = new();
				GameManager.client.Start(fileServerIp);
			}

			[SettingInteractableFunc("connectToFileServer")]
			public bool ConnectToFileServerInteractable() {
				return GameManager.client == null;
			}

			[SettingLocation("general", 120)]
			[SettingButton("hostFileServer")]
			public void HostFileServer() {
				GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
			}
		}
	}
}