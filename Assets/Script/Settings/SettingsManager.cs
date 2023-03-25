using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static class SettingsManager {
		// TODO: Use class with attributes instead of this mess

		public class SettingInfo {
			public string type;
			public object value;

			/// <summary>
			/// Used to set a variable to the value of this setting. Is called when the setting is changed.
			/// </summary>
			public Action<object> variableSetter;

			/// <summary>
			/// Returns whether the setting is interactable or not.
			/// </summary>
			public Func<bool> isInteractable;

			/// <summary>
			/// Called when the button is pressed (button setting type only).
			/// </summary>
			public Action buttonAction;

			/// <summary>
			/// If true, a space will be added above this setting in the setting menu.
			/// </summary>
			public bool spaceAbove;
		}

		private static OrderedDictionary settings = new();
		public static OrderedDictionary AllSettings => settings.AsReadOnly();

		private static Dictionary<string, JValue> savedValues = new();

		static SettingsManager() {
			ParseSettingsFile();

			// Get song folder location
			string songFolder = "";
			if (!savedValues.ContainsKey("songFolder")) {
				// Look for Clone Hero...
				var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				var cloneHeroPath = Path.Combine(documentsPath, $"Clone Hero{Path.DirectorySeparatorChar}Songs");
				var yargPath = Path.Combine(documentsPath, $"YARG{Path.DirectorySeparatorChar}Songs");

				if (Directory.Exists(cloneHeroPath)) {
					songFolder = cloneHeroPath;
				} else if (!Directory.Exists(yargPath)) {
					Directory.CreateDirectory(yargPath);
				}

				// And if not, create our own
				songFolder = yargPath;
			}

			// Song library settings
			Register("songFolder", new SettingInfo {
				type = "Folder",
				value = songFolder,
				isInteractable = () => GameManager.client == null,
				variableSetter = (obj) => {
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
					PlayerManager.globalCalibration = ((int) obj) / 1000f;
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

			// Save all setting default values to file
			foreach (DictionaryEntry entry in settings) {
				var name = (string) entry.Key;
				var info = (SettingInfo) entry.Value;

				// Skip buttons (they don't have values)
				if (info.type == "Button") {
					continue;
				}

				if (!savedValues.ContainsKey(name)) {
					savedValues.Add(name, new(info.value));
				}
			}
			SaveSettingsToFile();
		}

		private static void ParseSettingsFile() {
			try {
				var path = Path.Combine(Application.persistentDataPath, "settings.json");

				var file = File.ReadAllText(path);
				savedValues = JsonConvert.DeserializeObject<Dictionary<string, JValue>>(file);
			} catch (Exception e) {
				Debug.LogWarning($"Could not parse settings file: {e.Message}");
			}
		}

		private static void SaveSettingsToFile() {
			try {
				var path = Path.Combine(Application.persistentDataPath, "settings.json");

				var file = JsonConvert.SerializeObject(savedValues, Formatting.Indented);
				File.WriteAllText(path, file);
			} catch (Exception e) {
				Debug.LogWarning($"Could not save settings file: {e.Message}");
			}
		}

		private static void Register(string name, SettingInfo info) {
			if (settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} already exists!");
				return;
			}

			settings[name] = info;

			// Set value from saved values
			if (info.value != null && savedValues.ContainsKey(name)) {
				info.value = savedValues[name].ToObject(info.value.GetType());
			}

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

			// Save to file
			savedValues[name] = new(value);
			SaveSettingsToFile();

			Debug.Log($"Setting {name} to {value}.");
		}
	}
}