using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YARG.Settings {
	public class SettingsDropdown : MonoBehaviour {
		[SerializeField]
		private LocalizeStringEvent _dropdownName;
		[SerializeField]
		private TMP_Dropdown _dropdown;

		private IReadOnlyList<DropdownPreset> _presets;

		public IReadOnlyList<string> ModifiedSettings { get; private set; }

		public void SetInfo(PresetDropdownMetadata metadata) {
			_presets = metadata.DefaultPresets;
			ModifiedSettings = metadata.ModifiedSettings;

			// Set dropdown name
			_dropdownName.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = $"Dropdown.{metadata.DropdownName}"
			};

			// Set dropdown options
			_dropdown.options.Clear();
			foreach (var preset in metadata.DefaultPresets) {
				_dropdown.options.Add(new TMP_Dropdown.OptionData(preset.Name));
			}

			// Add custom option
			_dropdown.options.Add(new TMP_Dropdown.OptionData("<i>Custom</i>"));

			// Set value
			ForceUpdateValue();
		}

		public void OnValueChange() {
			if (_dropdown.value >= _presets.Count) {
				return;
			}

			var preset = _presets[_dropdown.value];

			foreach (var (name, value) in preset.Values) {
				// Set the setting value
				SettingsManager.SetSettingsByName(name, value);

				// Force update visuals
				SettingsMenu.Instance.UpdateSpecificSetting(name);
			}
		}

		public void ForceUpdateValue() {
			for (var i = 0; i < _presets.Count; i++) {
				var preset = _presets[i];

				// Check if the preset matches the current settings
				bool okay = true;
				foreach (var (name, value) in preset.Values) {
					if (!SettingsManager.GetSettingByName(name).IsSettingDataEqual(value)) {
						okay = false;
						break;
					}
				}

				if (!okay) {
					continue;
				}

				_dropdown.SetValueWithoutNotify(i);
				return;
			}

			_dropdown.SetValueWithoutNotify(_presets.Count);
		}
	}
}