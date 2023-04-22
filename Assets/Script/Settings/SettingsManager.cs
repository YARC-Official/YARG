using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class SettingsTab {
			public string name;
			public List<string> settings = new();
		}

		public static readonly List<SettingsTab> settingsTabs = new() {
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
					"ClapsInStarpower",
					"ReverbInStarpower",
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
			try {
				Settings = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(SettingsFile));
			} catch (Exception) {
				Settings = new SettingContainer();
			}
		}

		public static void SaveSetting() {
			File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings));
		}
	}
}