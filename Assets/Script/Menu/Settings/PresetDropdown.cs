using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Menu.Persistent;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private List<BasePreset> _presetsByIndex;

        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;

            _dropdown.options.Clear();

            // Add the defaults
            _presetsByIndex = _tab.SelectedContent.AddOptionsToDropdown(_dropdown);

            // Set index
            _dropdown.SetValueWithoutNotify(_presetsByIndex.IndexOf(_tab.SelectedPreset));
        }

        public void OnDropdownChange()
        {
            var preset = _presetsByIndex[_dropdown.value];

            _tab.SelectedPreset = preset;

            SettingsMenu.Instance.Refresh();
        }

        public void RenamePreset()
        {
            var preset = _presetsByIndex[_dropdown.value];

            if (preset.DefaultPreset) return;

            DialogManager.Instance.ShowRenameDialog("Rename Preset", value =>
            {
                _tab.SelectedContent.RenamePreset(preset, value);

                SettingsMenu.Instance.Refresh();
            });
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