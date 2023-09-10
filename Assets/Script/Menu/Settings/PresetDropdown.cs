using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private readonly List<BasePreset> _presetsByIndex = new();

        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;

            _dropdown.options.Clear();
            _presetsByIndex.Clear();

            // Add the defaults
            foreach (var preset in _tab.SelectedContent.DefaultBasePresets)
            {
                _dropdown.options.Add(new($"<color=#1CCFFF>{preset.Name}</color>"));
                _presetsByIndex.Add(preset);
            }

            // Add the customs
            foreach (var preset in _tab.SelectedContent.CustomBasePresets)
            {
                _dropdown.options.Add(new(preset.Name));
                _presetsByIndex.Add(preset);
            }

            // Set index
            _dropdown.SetValueWithoutNotify(_presetsByIndex.IndexOf(_tab.SelectedPreset));
        }

        public void OnDropdownChange()
        {
            var preset = _presetsByIndex[_dropdown.value];

            _tab.SelectedPreset = preset;

            SettingsMenu.Instance.Refresh();
        }

        public void CopyPreset()
        {
            var preset = _presetsByIndex[_dropdown.value];

            var copy = preset.CopyWithNewName($"Copy of {preset.Name}");
            _tab.SelectedContent.AddPreset(copy);
            _tab.SelectedPreset = copy;

            SettingsMenu.Instance.Refresh();
        }

        public void DeletePreset()
        {
            var preset = _presetsByIndex[_dropdown.value];

            if (preset.DefaultPreset) return;

            _tab.SelectedContent.DeletePreset(preset);
            _tab.ResetSelectedPreset();

            SettingsMenu.Instance.Refresh();
        }
    }
}