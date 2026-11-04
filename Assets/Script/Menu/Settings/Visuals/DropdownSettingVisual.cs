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

        public override void RefreshVisual()
        {
            var labels = new string[Setting.Count];
            for (int i = 0; i < Setting.Count; i++)
            {
                string valueString = Setting.IndexToString(i);
                if (Setting.Localizable)
                {
                    valueString = !IsPresetSetting
                        ? Localize.Key("Settings.Setting", UnlocalizedName, "Dropdown", valueString)
                        : Localize.Key("Settings.PresetSetting", UnlocalizedName, "Dropdown", valueString);
                }

                labels[i] = valueString;
            }

            if (Setting is ISearchableDropdownSetting)
            {
                if (!_dropdown.TryGetComponent(out SearchableDropdownMarker marker))
                {
                    marker = _dropdown.gameObject.AddComponent<SearchableDropdownMarker>();
                }

                marker.Initialize(_dropdown, labels, Setting.CurrentIndex, Setting.SelectIndex);
            }
            else
            {
                _dropdown.options.Clear();
                foreach (string label in labels)
                {
                    _dropdown.options.Add(new(label));
                }

                _dropdown.SetValueWithoutNotify(Setting.CurrentIndex);
            }

            bool hasMultipleOptions = Setting.Count > 1;
            _dropdown.interactable = hasMultipleOptions;
            var arrow = _dropdown.transform.Find("Arrow");
            if (arrow != null)
            {
                arrow.gameObject.SetActive(hasMultipleOptions);
            }
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
            if (_dropdown.TryGetComponent(out SearchableDropdownMarker marker) && marker.IsConfigured)
            {
                marker.SelectVisibleIndex(_dropdown.value);
            }
            else
            {
                Setting.SelectIndex(_dropdown.value);
            }
            RefreshVisual();
        }
    }
}
