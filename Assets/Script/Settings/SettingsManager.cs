using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class Tab {
			public string name;
			public string icon = "Generic";

			public string previewPath;

			public bool showInGame = false;
			public List<AbstractMetadata> settings = new();
		}

		public static readonly List<Tab> SETTINGS_TABS = new() {
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
			new() {
				name = "Graphics",
				icon = "Display",
				showInGame = true,
				previewPath = "SettingPreviews/TrackPreview",
				settings = {
					new HeaderMetadata("Framerate"),
					"VSync",
					"FpsCap",
					new HeaderMetadata("Graphics"),
					"LowQuality",
					"DisableBloom",
					new HeaderMetadata("Camera"),
					"TrackCamFOV",
					"TrackCamYPos",
					"TrackCamZPos",
					"TrackCamRot",
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
			} catch (Exception e) {
				Debug.LogException(e);
			}

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
			var method = typeof(SettingContainer).GetMethod(name);

			if (method == null) {
				throw new Exception($"The method `{name}` does not exist.");
			}

			method.Invoke(Settings, null);
		}

		public static Tab GetTabByName(string name) {
			foreach (var tab in SETTINGS_TABS) {
				if (tab.name == name) {
					return tab;
				}
			}

			return null;
		}
	}
}