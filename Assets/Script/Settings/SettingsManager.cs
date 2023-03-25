using System.Collections.Specialized;
using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static class SettingsManager {
		public class SettingInfo {
			public string type;
			public object value;

			/// <summary>
			/// Used to set a variable to the value of this setting. Is called when the setting is changed.
			/// </summary>
			public System.Action<object> variableSetter;

			/// <summary>
			/// Returns whether the setting is interactable or not.
			/// </summary>
			public System.Func<bool> isInteractable;

			/// <summary>
			/// Called when the button is pressed (button setting type only).
			/// </summary>
			public System.Action buttonAction;

			/// <summary>
			/// If true, a space will be added above this setting in the setting menu.
			/// </summary>
			public bool spaceAbove;
		}

		private static OrderedDictionary settings = new();
		public static OrderedDictionary AllSettings => settings.AsReadOnly();

		static SettingsManager() {
			// Song library settings
			Register("songFolder", new SettingInfo {
				type = "Folder",
				value = SongLibrary.songFolder,
				isInteractable = () => GameManager.client == null,
				variableSetter = (obj) => {
					SongLibrary.songFolder = (System.IO.DirectoryInfo) obj;

					if (MainMenu.Instance != null) {
						MainMenu.Instance.RefreshSongLibrary();
					}
				}
			});
			Register("refreshCache", new SettingInfo {
				type = "Button",
				isInteractable = () => GameManager.client == null,
				buttonAction = () => {
					MainMenu.Instance.RefreshCache();
				}
			});
			Register("exportOuvertSongs", new SettingInfo {
				type = "Button",
				buttonAction = () => {
					StandaloneFileBrowser.SaveFilePanelAsync("Save Song List", null, "songs", "json", path => {
						OuvertExport.ExportOuvertSongsTo(path);
					});
				}
			});

			// Calibration settings
			Register("calibrationNumber", new SettingInfo {
				spaceAbove = true,

				type = "Number",
				value = (int) (PlayerManager.globalCalibration * 1000f),
				variableSetter = (obj) => {
					PlayerManager.globalCalibration = (int) obj / 1000f;
				}
			});
			Register("calibrate", new SettingInfo {
				type = "Button",
				buttonAction = () => {
					if (PlayerManager.players.Count > 0) {
						GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
					}
				}
			});

			// Toggle settings
			Register("lowQuality", new SettingInfo {
				spaceAbove = true,

				type = "Toggle",
				value = false,
				variableSetter = obj => {
					QualitySettings.SetQualityLevel((bool) obj ? 0 : 1, true);
				}
			});
			Register("showHitWindow", new SettingInfo {
				type = "Toggle",
				value = false
			});
			Register("useAudioTime", new SettingInfo {
				type = "Toggle",
				value = false
			});
			Register("vsync", new SettingInfo {
				type = "Toggle",
				value = true,
				variableSetter = obj => {
					QualitySettings.vSyncCount = (bool) obj ? 1 : 0;
				}
			});
		}

		private static void Register(string name, SettingInfo info) {
			if (settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} already exists!");
				return;
			}

			settings[name] = info;

			// Call setter when registered
			info.variableSetter?.Invoke(info.value);
		}

		public static object GetSettingValue(string name) {
			if (name == null || !settings.Contains(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
				return null;
			}

			return ((SettingInfo) settings[name]).value;
		}

		public static T GetSettingValue<T>(string name) {
			var obj = GetSettingValue(name);

			if (obj == null) {
				return default;
			}

			return (T) obj;
		}

		public static SettingInfo GetSettingInfo(string name) {
			if (name == null || !settings.Contains(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
				return null;
			}

			return (SettingInfo) settings[name];
		}

		public static void SetSettingValue(string name, object value) {
			if (name == null || !settings.Contains(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
				return;
			}

			var setting = (SettingInfo) settings[name];
			setting.value = value;

			// Set bound variable
			setting.variableSetter?.Invoke(value);

			Debug.Log($"Setting {name} to {value}.");
		}
	}
}