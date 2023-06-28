using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals
{
    public class DropdownSettingVisual : AbstractSettingVisual<DropdownSetting>
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
                _dropdown.options.Add(new(new LocalizedString
                {
                    TableReference = "Settings", TableEntryReference = $"{SettingName}.{name}"
                }.GetLocalizedString()));
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