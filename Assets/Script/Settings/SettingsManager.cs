using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class SettingsTab {
			public string name;
			public string icon = "Generic";

			public bool showInGame = false;
			public List<AbstractMetadata> settings = new();
		}

		public static readonly List<SettingsTab> SETTINGS_TABS = new() {
			new() {
				name = "General",
				settings = {
					new HeaderMetadata("SongManagement"),
					new ButtonRowMetadata("OpenSongFolderManager"),
					new ButtonRowMetadata("ExportOuvertSongs"),
					new ButtonRowMetadata("CopyCurrentSongTextFilePath"),
					new HeaderMetadata("Other"),
					"CalibrationNumber",
					"ShowHitWindow",
					"UseCymbalModelsInFiveLane",
					"AmIAwesome"
				}
			},
			new() {
				name = "Graphics",
				icon = "Display",
				settings = {
					new HeaderMetadata("Framerate"),
					"VSync",
					"FpsCap",
					new HeaderMetadata("Graphics"),
					"LowQuality",
					"DisableBloom",
					"HighFovCamera",
				}
			},
			new() {
				name = "Engine",
				icon = "Gameplay",
				settings = {
					"NoKicks",
					"AntiGhosting"
				}
			},
			new() {
				name = "Sound",
				icon = "Sound",
				showInGame = true,
				settings = {
					new HeaderMetadata("Volume"),
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
					new HeaderMetadata("Other"),
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
			try {
				Settings = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(SettingsFile));
			} catch { }

			// If null, recreate
			Settings ??= new SettingContainer();
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

			if (value == null) {
				Debug.LogWarning($"`{name}` has a value of null. This might create errors.");
			}

			return (ISettingType) value;
		}

		public static void InvokeButton(string name) {
			name = name[1..];
			var method = typeof(SettingContainer).GetMethod(name);

			if (method == null) {
				throw new Exception($"The method `{name}` does not exist.");
			}

			method.Invoke(Settings, null);
		}
	}
}