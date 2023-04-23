using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Settings.SettingTypes;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class SettingsTab {
			public string name;
			public List<string> settings = new();
		}

		public static readonly List<SettingsTab> SETTINGS_TABS = new() {
			new() {
				name = "General",
				settings = {
					"CalibrationNumber",
					"ShowHitWindow",
					"UseCymbalModelsInFiveLane",
					"AmIAwesome"
				}
			},
			new() {
				name = "Graphics",
				settings = {
					"VSync",
					"FpsCap",
					"LowQuality",
					"DisableBloom",
					"HighFovCamera",
				}
			},
			new() {
				name = "Engine",
				settings = {
					"NoKicks",
					"AntiGhosting"
				}
			},
			new() {
				name = "Sound",
				settings = {
					"MasterMusicVolume",
					"GuitarVolume",
					"RhythmVolume",
					"BassVolume",
					"KeysVolume",
					"DrumsVolume",
					"VocalsVolume",
					"SongVolume",
					"CrowdVolume",
					"SfxVolume",
					"VocalMonitoring",
					"MuteOnMiss",
					"UseStarpowerFx",
					// "ClapsInStarpower",
					// "ReverbInStarpower",
					"UseChipmunkSpeed",
				}
			},
		};

		private static string SettingsFile => Path.Combine(GameManager.PersistentDataPath, "settings.json");

		public static SettingContainer Settings {
			get;
			private set;
		} = null;

		public static void LoadSettings() {
			// Create settings container
			// try {
			// 	Settings = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(SettingsFile));
			// } catch (Exception) {
			Settings = new SettingContainer();
			// }
		}

		public static void SaveSettings() {
			File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings));
		}

		public static void DeleteSettings() {
			try {
				File.Delete(SettingsFile);
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		public static ISettingType GetSettingByName(string name) {
			var field = typeof(SettingContainer).GetProperty(name);

			if (field == null) {
				throw new Exception($"The field `{name}` does not exist.");
			}

			var value = field.GetValue(Settings);
			return (ISettingType) value;
		}

		public static ISettingType GetSettingTypeByName(string name) {
			var field = typeof(SettingContainer).GetProperty(name);

			if (field == null) {
				throw new Exception($"The field `{name}` does not exist.");
			}

			var value = field.GetValue(Settings);
			return (ISettingType) value;
		}
	}
}