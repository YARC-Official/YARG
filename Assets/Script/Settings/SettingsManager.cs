using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class Tab {
			public string Name;
			public string Icon = "Generic";

			public string PreviewPath;

			public bool ShowInPlayMode;
			public List<AbstractMetadata> Settings = new();
		}

		public static readonly List<Tab> SettingsTabs = new() {
			new() {
				Name = "General",
				Settings = {
					new HeaderMetadata("FileManagement"),
					new ButtonRowMetadata("OpenSongFolderManager"),
					new ButtonRowMetadata("ExportOuvertSongs"),
					new ButtonRowMetadata("CopyCurrentSongTextFilePath", "CopyCurrentSongJsonFilePath"),
					new HeaderMetadata("Venues"),
					new ButtonRowMetadata("OpenVenueFolder"),
					"DisablePerSongBackgrounds",
					new HeaderMetadata("Calibration"),
					new ButtonRowMetadata("OpenCalibrator"),
					"AudioCalibration",
					new HeaderMetadata("Other"),
					"ShowHitWindow",
					"UseCymbalModelsInFiveLane",
					"AmIAwesome"
				}
			},
			new() {
				Name = "Sound",
				Icon = "Sound",
				ShowInPlayMode = true,
				Settings = {
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
					"PreviewVolume",
					"MusicPlayerVolume",
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
				Name = "Graphics",
				Icon = "Display",
				ShowInPlayMode = true,
				PreviewPath = "SettingPreviews/TrackPreview",
				Settings = {
					new HeaderMetadata("Framerate"),
					"VSync",
					"FpsStats",
					"FpsCap",
					new HeaderMetadata("Graphics"),
					"LowQuality",
					"DisableBloom",
					new HeaderMetadata("Camera"),
					new ButtonRowMetadata("ResetCameraSettings"),
					"TrackCamFOV",
					"TrackCamYPos",
					"TrackCamZPos",
					"TrackCamRot",
					new HeaderMetadata("Other"),
					"DisableTextNotifications"
				}
			},
			new() {
				Name = "Engine",
				Icon = "Gameplay",
				Settings = {
					"NoKicks",
					"AntiGhosting"
				}
			},
		};

		private static string SettingsFile => Path.Combine(GameManager.PersistentDataPath, "settings.json");

		public static SettingContainer Settings { get; private set; }

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
			return SettingsTabs.FirstOrDefault(tab => tab.Name == name);
		}
	}
}