using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Settings {
	public static class SettingsManager {
		public static Dictionary<string, object> settings = new();

		static SettingsManager() {
			// Default setting values
			SetSettingValue("songFolder", SongLibrary.songFolder);
		}

		public static object GetSettingValue(string name) {
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
			settings[name] = value;
		}
	}
}