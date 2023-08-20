using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Menu.Navigation;
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

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Down, "Next", () =>
                {
                    int newValue = _dropdown.value + 1;
                    if (newValue >= _dropdown.options.Count)
                    {
                        newValue = 0;
                    }

                    _dropdown.value = newValue;
                }),
                new NavigationScheme.Entry(MenuAction.Up, "Previous", () =>
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
            Setting.Data = Setting.PossibleValues[_dropdown.value];
            RefreshVisual();
        }
    }
}