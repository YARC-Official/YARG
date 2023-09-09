using System;
using TMPro;
using UnityEngine;
using YARG.Settings.Customization;

namespace YARG.Menu.Settings
{
    public class PresetTypeDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private CustomContent[] _presetTypes;
        private Action<CustomContent> _action;

        public void Initialize(CustomContent[] presetTypes, CustomContent selected, Action<CustomContent> action)
        {
            _presetTypes = presetTypes;
            _action = action;

            // Add the options (in order)
            _dropdown.options.Clear();
            foreach (var type in presetTypes)
            {
                _dropdown.options.Add(new(type.GetType().Name));
            }

            // Set index
            _dropdown.SetValueWithoutNotify(Array.IndexOf(presetTypes, selected));
        }

        public void OnDropdownChange()
        {
            var type = _presetTypes[_dropdown.value];
            _action?.Invoke(type);
        }
    }
}