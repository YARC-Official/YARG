using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class DropdownSettingVisual : BaseSettingVisual<IDropdownSetting>
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        protected override void RefreshVisual()
        {
            // Add the options (in order)
            _dropdown.options.Clear();
            for (int i = 0; i < Setting.Count; i++)
            {
                string valueString = Setting.IndexToString(i);
                if (Setting.Localizable)
                {
                    valueString = !IsPresetSetting
                        ? Localize.Key("Settings.Setting", UnlocalizedName, "Dropdown", valueString)
                        : Localize.Key("Settings.PresetSetting", UnlocalizedName, "Dropdown", valueString);
                }

                _dropdown.options.Add(new(valueString));
            }

            // Select the right option
            _dropdown.SetValueWithoutNotify(Setting.CurrentIndex);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Next", () =>
                {
                    int newValue = _dropdown.value + 1;
                    if (newValue >= _dropdown.options.Count)
                    {
                        newValue = 0;
                    }

                    _dropdown.value = newValue;
                }),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Previous", () =>
                {
                    int newValue = _dropdown.value - 1;
                    if (newValue < 0)
                    {
                        newValue = _dropdown.options.Count - 1;
                    }

                    _dropdown.value = newValue;
                })
            }, true);
        }

        public void OnDropdownChange()
        {
            Setting.SelectIndex(_dropdown.value);
            RefreshVisual();
        }
    }
}