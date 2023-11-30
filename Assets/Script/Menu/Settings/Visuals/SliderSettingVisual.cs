using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class SliderSettingVisual : BaseSettingVisual<SliderSetting>
    {
        [SerializeField]
        private ValueSlider _slider;

        // Unity sucks -_-
        private bool _ignoreCallback;

        protected override void OnSettingInit()
        {
            _ignoreCallback = true;
            _slider.MinimumValue = Setting.Min;
            _slider.MaximumValue = Setting.Max;
            _ignoreCallback = false;

            base.OnSettingInit();
        }

        protected override void RefreshVisual()
        {
            _slider.SetValueWithoutNotify(Setting.Data);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Increase", () =>
                {
                    var range = Setting.Max - Setting.Min;
                    Setting.Data += range / 30f;

                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Decrease", () =>
                {
                    var range = Setting.Max - Setting.Min;
                    Setting.Data -= range / 30f;

                    RefreshVisual();
                })
            }, true);
        }

        public void OnValueChange()
        {
            if (_ignoreCallback) return;

            Setting.Data = _slider.Value;
            RefreshVisual();
        }
    }
}