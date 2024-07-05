using System;
using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetTypeDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private CustomContent[] _presetTypes;
        private PresetsTab _tab;

        public void Initialize(PresetsTab tab, CustomContent[] presetTypes)
        {
            _tab = tab;
            _presetTypes = presetTypes;

            // Add the options (in order)
            _dropdown.options.Clear();
            foreach (var type in presetTypes)
            {
                var name = Localize.Key("Settings.PresetType", type.GetType().Name);
                _dropdown.options.Add(new(name));
            }

            // Set index
            _dropdown.SetValueWithoutNotify(Array.IndexOf(presetTypes, tab.SelectedContent));
        }

        public void OnDropdownChange()
        {
            _tab.SelectedContent = _presetTypes[_dropdown.value];
            SettingsMenu.Instance.Refresh();
        }

        public void OpenPresetFolder()
        {
            var customContent = _presetTypes[_dropdown.value];
            FileExplorerHelper.OpenFolder(customContent.FullContentDirectory);
        }
    }
}