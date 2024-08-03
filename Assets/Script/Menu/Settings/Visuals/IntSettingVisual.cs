using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class IntSettingVisual : BaseSettingVisual<IntSetting>
    {
        [SerializeField]
        private TMP_InputField _inputField;

        protected override void RefreshVisual()
        {
            _inputField.text = Setting.Value.ToString(CultureInfo.InvariantCulture);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () =>
                {
                    Setting.Value++;
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () =>
                {
                    Setting.Value--;
                    RefreshVisual();
                })
            }, true);
        }

        public void OnTextFieldChange()
        {
            try
            {
                int value = int.Parse(_inputField.text, CultureInfo.InvariantCulture);
                value = Mathf.Clamp(value, Setting.Min, Setting.Max);

                Setting.Value = value;
            }
            catch
            {
                // Ignore error
            }

            RefreshVisual();
        }
    }
}