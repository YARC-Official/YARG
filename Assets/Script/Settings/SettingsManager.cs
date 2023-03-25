using System.Collections.Specialized;
using SFB;
using UnityEngine;
using YARG.Serialization;
using YARG.UI;

namespace YARG.Settings {
	public static class SettingsManager {
		public class SettingInfo {
			public object value;
			public string type;

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
		}

		private static OrderedDictionary settings = new();
		public static OrderedDictionary AllSettings => settings.AsReadOnly();

		static SettingsManager() {
			// Song library settings

			Register("songFolder", new SettingInfo {
				value = SongLibrary.songFolder,
				type = "Folder",
				isInteractable = () => GameManager.client == null,
				variableSetter = (obj) => {
					SongLibrary.songFolder = (System.IO.DirectoryInfo) obj;
					MainMenu.Instance.RefreshSongLibrary();
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

		}

		private static void Register(string name, SettingInfo info) {
			if (settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} already exists!");
				return;
			}

			settings[name] = info;
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