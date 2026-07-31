using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public class IntPauseSetting : BasePauseSetting<IntSetting>
    {
        [Space]
        [SerializeField]
        private TMP_InputField _inputField;

        public override void Initialize(string settingName, IntSetting setting)
        {
            base.Initialize(settingName, setting);

            _inputField.text = setting.Value.ToString(CultureInfo.InvariantCulture);
            _inputField.onValueChanged.AddListener(OnValueChange);
            _inputField.onEndEdit.AddListener(OnEndEdit);
        }

        protected override NavigationScheme GetNavigationScheme()
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

        private void OnValueChange(string text)
        {
            // Keep incomplete input such as a leading minus sign while the user is typing.
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return;
            }

            Setting.Value = value;
        }

        private void OnEndEdit(string _)
        {
            // Restore invalid input and show any clamping performed by the setting.
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            _inputField.text = Setting.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
