using System;
using TMPro;
using UnityEngine;

namespace YARG.Menu.Settings
{
    public class PresetTypeDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private Type[] _presetTypes;
        private Action<Type> _action;

        public void Initialize(Type[] presetTypes, Type selected, Action<Type> action)
        {
            _presetTypes = presetTypes;
            _action = action;

            // Add the options (in order)
            _dropdown.options.Clear();
            foreach (var type in presetTypes)
            {
                _dropdown.options.Add(new(type.Name));
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