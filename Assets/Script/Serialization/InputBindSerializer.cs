using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input;

namespace YARG.Serialization {
	public static class InputBindSerializer {
		private class InputBindSave {
			public string inputStrategy;
			public string deviceName;
			public Dictionary<string, string> binds = new();
		}

		private static List<InputBindSave> inputBindSaves = new();

		static InputBindSerializer() {
			// Load from JSON
			try {
				var json = File.ReadAllText(Path.Combine(Application.persistentDataPath, "inputBinds.json"));
				inputBindSaves = JsonConvert.DeserializeObject<List<InputBindSave>>(json);
			} catch (Exception) {
				Debug.LogWarning("Failed to load input binds from JSON. Ignoring.");
			}
		}

		public static void LoadBindsFromSave(InputStrategy inputStrategy) {
			// Look from correct bind (using input strategy and device)
			InputBindSave inputBindSave = null;
			foreach (InputBindSave bindSave in inputBindSaves) {
				if (bindSave.deviceName != inputStrategy.InputDevice.name) {
					continue;
				}

				if (bindSave.inputStrategy != inputStrategy.GetType().Name) {
					continue;
				}

				inputBindSave = bindSave;
				break;
			}

			if (inputBindSave == null) {
				return;
			}

			// Set binds
			foreach (var binding in inputStrategy.GetMappingNames()) {
				if (!inputBindSave.binds.ContainsKey(binding)) {
					continue;
				}

				var control = InputControlPath.TryFindControl(inputStrategy.InputDevice, inputBindSave.binds[binding]);
				inputStrategy.SetMappingInputControl(binding, control);
			}
		}

		public static void SaveBindsFromInputStrategy(InputStrategy inputStrategy) {
			// Look for existing bind save
			InputBindSave inputBindSave = null;
			foreach (InputBindSave bindSave in inputBindSaves) {
				if (bindSave.deviceName != inputStrategy.InputDevice.name) {
					continue;
				}

				if (bindSave.inputStrategy != inputStrategy.GetType().Name) {
					continue;
				}

				inputBindSave = bindSave;
				break;
			}

			if (inputBindSave != null) {
				inputBindSaves.Remove(inputBindSave);
			}

			// Create new bind save if none found
			inputBindSave = new InputBindSave {
				inputStrategy = inputStrategy.GetType().Name,
				deviceName = inputStrategy.InputDevice.name
			};
			inputBindSaves.Add(inputBindSave);

			// Save binds
			inputBindSave.binds.Clear();
			foreach (var binding in inputStrategy.GetMappingNames()) {
				var control = inputStrategy.GetMappingInputControl(binding);
				if (control == null) {
					continue;
				}

				inputBindSave.binds.Add(binding, control.path);
			}

			// Save to JSON
			SaveToJsonFile();
		}

		private static void SaveToJsonFile() {
			var json = JsonConvert.SerializeObject(inputBindSaves);
			File.WriteAllText(Path.Combine(Application.persistentDataPath, "inputBinds.json"), json);
		}
	}
}