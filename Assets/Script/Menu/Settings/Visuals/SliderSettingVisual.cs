using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class SliderSettingVisual : BaseSettingVisual<SliderSetting>
    {
        private static readonly Color READONLY_DISABLED_GREY = new(0.6f, 0.6f, 0.6f, 1f);

        [SerializeField]
        private ValueSlider _slider;

        private bool _defaultsCaptured;
        private Color _fillDefault;
        private Color _disabledColorDefault;

        // Unity sucks -_-
        private bool _ignoreCallback;

        public override void SetEditable(bool editable, bool dim = true)
        {
            var slider = _slider.GetComponentInChildren<Slider>();
            if (slider != null)
            {
                if (!_defaultsCaptured)
                {
                    _defaultsCaptured = true;
                    _fillDefault = slider.fillRect != null
                        ? slider.fillRect.GetComponent<Image>().color
                        : Color.white;
                    _disabledColorDefault = slider.colors.disabledColor;
                }

                // Keep the disabled handle opaque so the slider shape stays hidden.
                var colors = slider.colors;
                colors.disabledColor = editable
                    ? _disabledColorDefault
                    : READONLY_DISABLED_GREY;
                slider.colors = colors;
            }

            base.SetEditable(editable, dim);

            if (slider?.fillRect == null)
            {
                return;
            }

            var fill = slider.fillRect.GetComponent<Image>();
            if (editable)
            {
                fill.color = _fillDefault;
            }
            else
            {
                var fillGrey = RuntimeNavigatable.DimmedSelectionGrey;
                fillGrey.a = _fillDefault.a;
                fill.color = fillGrey;
            }
        }

        protected override void OnSettingInit()
        {
            _ignoreCallback = true;
            _slider.MinimumValue = Setting.Min;
            _slider.MaximumValue = Setting.Max;

            _ignoreCallback = false;

            base.OnSettingInit();
        }

        public override void RefreshVisual()
        {
            _slider.SetValueWithoutNotify(Setting.Value);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () =>
                {
                    var range = Setting.Max - Setting.Min;
                    Setting.Value += range / 20f;

                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () =>
                {
                    var range = Setting.Max - Setting.Min;
                    Setting.Value -= range / 20f;

                    RefreshVisual();
                })
            }, true);
        }

        public void OnValueChange()
        {
            if (_ignoreCallback) return;

            Setting.Value = _slider.Value;
            RefreshVisual();
        }
    }
}
