using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Settings.Metadata;
using YARG.Settings.Types;
using YARG.Util;

namespace YARG.Settings {
	public static partial class SettingsManager {
		public class Tab {
			public string Name;
			public string Icon = "Generic";

			public string PreviewPath;

			public bool ShowInPlayMode;
			public List<AbstractMetadata> Settings = new();
		}

		public static SettingContainer Settings { get; private set; }

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
					"KickBounce",
					"ShowCursorTimer",
					"PressThreshold",
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
					new HeaderMetadata("Input"),
					"MicrophoneSensitivity",
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
					new HeaderMetadata("Display"),
					"VSync",
					"FpsCap",
					"FullscreenMode",
					"Resolution",
					"FpsStats",
					new HeaderMetadata("Graphics"),
					"LowQuality",
					"DisableBloom",
					new HeaderMetadata("Camera"),
					new PresetDropdownMetadata("CameraPresets", new[] {
						"TrackCamFOV",
						"TrackCamYPos",
						"TrackCamZPos",
						"TrackCamRot",
						"TrackFadePosition",
						"TrackFadeSize",
					}, new() {
						new DropdownPreset("Default", new() {
							{ "TrackCamFOV", 55f },
							{ "TrackCamYPos", 2.66f },
							{ "TrackCamZPos", 1.14f },
							{ "TrackCamRot", 24.12f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("High FOV", new() {
							{ "TrackCamFOV", 60f },
							{ "TrackCamYPos", 2.66f },
							{ "TrackCamZPos", 1.27f },
							{ "TrackCamRot", 24.12f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("The Band 1", new() {
							{ "TrackCamFOV", 47.84f },
							{ "TrackCamYPos", 2.43f },
							{ "TrackCamZPos", 1.42f },
							{ "TrackCamRot", 26f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("The Band 2", new() {
							{ "TrackCamFOV", 44.97f },
							{ "TrackCamYPos", 2.66f },
							{ "TrackCamZPos", 0.86f },
							{ "TrackCamRot", 24.12f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("The Band 3", new() {
							{ "TrackCamFOV", 57.29f },
							{ "TrackCamYPos", 2.22f },
							{ "TrackCamZPos", 1.61f },
							{ "TrackCamRot", 23.65f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("The Band 4", new() {
							{ "TrackCamFOV", 62.16f },
							{ "TrackCamYPos", 2.56f },
							{ "TrackCamZPos", 1.20f },
							{ "TrackCamRot", 19.43f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("Hero 2", new() {
							{ "TrackCamFOV", 58.15f },
							{ "TrackCamYPos", 1.82f },
							{ "TrackCamZPos", 1.50f },
							{ "TrackCamRot", 12.40f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.5f },
						}),
						new DropdownPreset("Hero 3", new() {
							{ "TrackCamFOV", 52.71f },
							{ "TrackCamYPos", 2.17f },
							{ "TrackCamZPos", 1.14f },
							{ "TrackCamRot", 15.21f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.5f },
						}),
						new DropdownPreset("Hero Traveling the World", new() {
							{ "TrackCamFOV", 53.85f },
							{ "TrackCamYPos", 1.97f },
							{ "TrackCamZPos", 1.52f },
							{ "TrackCamRot", 16.62f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.5f },
						}),
						new DropdownPreset("Hero Live", new() {
							{ "TrackCamFOV", 62.16f },
							{ "TrackCamYPos", 2.40f },
							{ "TrackCamZPos", 1.42f },
							{ "TrackCamRot", 21.31f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.25f },
						}),
						new DropdownPreset("Clone", new() {
							{ "TrackCamFOV", 55f },
							{ "TrackCamYPos", 2.07f },
							{ "TrackCamZPos", 1.51f },
							{ "TrackCamRot", 17.09f },
							{ "TrackFadePosition", 3f },
							{ "TrackFadeSize", 1.5f },
						})
					}),
					"TrackCamFOV",
					"TrackCamYPos",
					"TrackCamZPos",
					"TrackCamRot",
					"TrackFadePosition",
					"TrackFadeSize",
					new HeaderMetadata("Other"),
					"DisableTextNotifications",
					"LyricBackground"
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

		private static string SettingsFile => Path.Combine(PathHelper.PersistentDataPath, "settings.json");

		public static void LoadSettings() {
			SettingContainer.IsLoading = true;

			// Create settings container
			try {
				Settings = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(SettingsFile));
			} catch (Exception e) {
				Debug.LogException(e);
			}

			// If null, recreate
			Settings ??= new SettingContainer();

			SettingContainer.IsLoading = false;

			// Now that we're done loading, call all of the callbacks
			var fields = typeof(SettingContainer).GetProperties();
			foreach (var field in fields) {
				var value = field.GetValue(Settings);

				if (value is not ISettingType settingType) {
					continue;
				}

				settingType.ForceInvokeCallback();
			}
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

		public static void SetSettingsByName(string name, object value) {
			var settingInfo = GetSettingByName(name);

			if (settingInfo.DataType != value.GetType()) {
				throw new Exception($"The setting `{name}` is of type {settingInfo.DataType}, not {value.GetType()}.");
			}

			settingInfo.DataAsObject = value;
		}
	}
}