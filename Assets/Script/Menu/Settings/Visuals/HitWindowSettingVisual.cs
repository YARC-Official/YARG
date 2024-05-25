using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class HitWindowSettingVisual : BaseSettingVisual<HitWindowSetting>
    {
        [SerializeField]
        private GameObject _dynamicContainer;
        [SerializeField]
        private GameObject _constantContainer;

        [Space]
        [SerializeField]
        private DurationInputField _minField;
        [SerializeField]
        private DurationInputField _maxField;

        [Space]
        [SerializeField]
        private DurationInputField _constantField;

        protected override void OnSettingInit()
        {
            _minField.PreferredUnit = DurationInputField.Unit.Milliseconds;
            _maxField.PreferredUnit = DurationInputField.Unit.Milliseconds;
            _constantField.PreferredUnit = DurationInputField.Unit.Milliseconds;

            base.OnSettingInit();
        }

        protected override void RefreshVisual()
        {
            if (Setting.Value.IsDynamic)
            {
                _dynamicContainer.SetActive(true);
                _constantContainer.SetActive(false);

                _minField.Duration = Setting.Value.MinWindow;
                _maxField.Duration = Setting.Value.MaxWindow;
            }
            else
            {
                _dynamicContainer.SetActive(false);
                _constantContainer.SetActive(true);

                // We should use max here because that's what the
                // hit window snaps to when using a constant size.
                _constantField.Duration = Setting.Value.MaxWindow;
            }
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish
            }, true);
        }

        public void OnTextFieldChange()
        {
            var window = Setting.Value;

            if (Setting.Value.IsDynamic)
            {
                window.MinWindow = _minField.Duration;
                window.MaxWindow = _maxField.Duration;
            }
            else
            {
                // We should use both here though to prevent the minimum from being higher than the max
                window.MinWindow = _constantField.Duration;
                window.MaxWindow = _constantField.Duration;
            }

            // Since the hit window is a reference type, we can just do this
            Setting.ForceInvokeCallback();
        }
    }
}