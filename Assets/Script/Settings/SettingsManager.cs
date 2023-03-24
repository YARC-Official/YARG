using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Settings {
	public static class SettingsManager {
		private static Dictionary<string, string> settingsType = new();
		public static Dictionary<string, object> settings = new();

		public static IReadOnlyDictionary<string, string> AllSettings => settingsType;

		static SettingsManager() {
			// Default setting values
			RegisterSetting("songFolder", SongLibrary.songFolder, "Folder");
		}

		private static void RegisterSetting(string name, object def, string type) {
			settings[name] = def;
			settingsType[name] = type;
		}

		public static object GetSettingValue(string name) {
			if (name == null || !settingsType.ContainsKey(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
			}

			if (settings.TryGetValue(name, out var s)) {
				return s;
			}

			return null;
		}

		public static T GetSettingValue<T>(string name) {
			var obj = GetSettingValue(name);

			if (obj == null) {
				return default;
			}

			return (T) obj;
		}

		public static void SetSettingValue(string name, object value) {
			if (name == null || !settingsType.ContainsKey(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
			}

			settings[name] = value;

			Debug.Log($"Setting {name} to {value}.");
		}
	}
}