using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class DurationSettingVisual : BaseSettingVisual<DurationSetting>
    {
        [SerializeField]
        private DurationInputField _inputField;

        protected override void OnSettingInit()
        {
            _inputField.PreferredUnit = Setting.PreferredUnit;
            _inputField.MaxValue = Setting.Max;

            base.OnSettingInit();
        }

        protected override void RefreshVisual()
        {
            _inputField.Duration = Setting.Value;
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () =>
                {
                    Setting.Value += DurationInputField.GetMultiplierForUnit(Setting.PreferredUnit);
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () =>
                {
                    Setting.Value -= DurationInputField.GetMultiplierForUnit(Setting.PreferredUnit);
                    RefreshVisual();
                })
            }, true);
        }

        public void OnTextFieldChange()
        {
            Setting.Value = _inputField.Duration;
        }
    }
}