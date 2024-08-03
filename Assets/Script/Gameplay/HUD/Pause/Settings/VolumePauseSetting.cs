using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public class VolumePauseSetting : BasePauseSetting<VolumeSetting>
    {
        [Space]
        [SerializeField]
        private Slider _slider;
        [SerializeField]
        private TextMeshProUGUI _value;

        public override void Initialize(string settingName, VolumeSetting setting)
        {
            base.Initialize(settingName, setting);

            _slider.SetValueWithoutNotify(setting.Value);
            _value.text = Localize.Percent(setting.Value);
        }

        protected override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () =>
                {
                    Setting.Value += 1f / 20f;

                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () =>
                {
                    Setting.Value -= 1f / 20f;

                    RefreshVisual();
                })
            }, true);
        }

        public void OnValueChange()
        {
            Setting.Value = _slider.value;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            _slider.value = Setting.Value;
            _value.text = Localize.Percent(_slider.value);
        }
    }
}