using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Settings {
	public static class SettingsManager {
		public class SettingInfo {
			public object value;
			public string type;
		}

		private static Dictionary<string, SettingInfo> settings = new();

		public static IReadOnlyDictionary<string, SettingInfo> AllSettings => settings;

		static SettingsManager() {
			// Default setting values
			RegisterSetting("songFolder", SongLibrary.songFolder, "Folder");
		}

		private static void RegisterSetting(string name, object def, string type) {
			settings[name] = new SettingInfo {
				value = def,
				type = type
			};
		}

		public static object GetSettingValue(string name) {
			if (name == null || !settings.ContainsKey(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
				return null;
			}

			if (settings.TryGetValue(name, out var s)) {
				return s.value;
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
			if (name == null || !settings.ContainsKey(name)) {
				Debug.LogWarning($"{name} does not exist in the registered settings!");
				return;
			}

			settings[name].value = value;

			Debug.Log($"Setting {name} to {value}.");
		}
	}
}