using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers;
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
                _dropdown.options.Add(new(LocaleHelper.LocalizeString(
                    "Settings", $"Dropdown.{Tab}.{UnlocalizedName}.{Setting.IndexToString(i)}")));
            }

            // Select the right option
            _dropdown.SetValueWithoutNotify(Setting.CurrentIndex);
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
            Setting.SelectIndex(_dropdown.value);
            RefreshVisual();
        }
    }
}