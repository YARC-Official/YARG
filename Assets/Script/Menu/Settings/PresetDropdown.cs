using TMPro;
using UnityEngine;
using YARG.Settings.Customization;

namespace YARG.Menu.Settings
{
    public class PresetDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private CustomContent _contentContainer;

        private int _customPresetsStart;

        public void Initialize(CustomContent contentContainer)
        {
            _contentContainer = contentContainer;

            _dropdown.options.Clear();

            // Add the defaults
            foreach (var name in _contentContainer.DefaultPresetNames)
            {
                _dropdown.options.Add(new($"<color=#1CCFFF>{name}</color>"));
            }

            // Add the customs
            _customPresetsStart = _dropdown.options.Count;
            foreach (var name in _contentContainer.CustomPresetNames)
            {
                _dropdown.options.Add(new(name));
            }

            // Set index
            _dropdown.SetValueWithoutNotify(0);
        }

        public void OnDropdownChange()
        {
        }
    }
}