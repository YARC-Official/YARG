using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class DropdownSettingVisual : BaseSettingVisual<DropdownSetting>
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        protected override void OnSettingInit()
        {
            RefreshVisual();
        }

        public override void RefreshVisual()
        {
            // Add the options (in order), and get enum indices
            _dropdown.options.Clear();
            foreach (var name in Setting.PossibleValues)
            {
                _dropdown.options.Add(new(LocaleHelper.LocalizeString("Settings", $"{SettingName}.{name}")));
            }

            // Select the right option
            _dropdown.SetValueWithoutNotify(Setting.IndexOfOption(Setting.Data));
        }

        public void OnDropdownChange()
        {
            Setting.Data = Setting.PossibleValues[_dropdown.value];
            RefreshVisual();
        }
    }
}