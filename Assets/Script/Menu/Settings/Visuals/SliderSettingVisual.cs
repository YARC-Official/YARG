using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class SliderSettingVisual : BaseSettingVisual<SliderSetting>
    {
        [SerializeField]
        private Slider _slider;

        [SerializeField]
        private TMP_InputField _inputField;

        // Unity sucks -_-
        private bool _ignoreCallback = false;

        protected override void OnSettingInit()
        {
            _ignoreCallback = true;

            _slider.minValue = Setting.Min;
            _slider.maxValue = Setting.Max;

            _ignoreCallback = false;

            RefreshVisual();
        }

        public override void RefreshVisual()
        {
            _slider.SetValueWithoutNotify(Setting.Data);
            _inputField.text = Setting.Data.ToString("N2", CultureInfo.InvariantCulture);
        }

        public void OnSliderChange()
        {
            if (_ignoreCallback)
            {
                return;
            }

            Setting.Data = _slider.value;
            RefreshVisual();
        }

        public void OnTextChange()
        {
            string text = _inputField.text;

            try
            {
                Setting.Data = float.Parse(text, CultureInfo.InvariantCulture);
            }
            catch
            {
            }

            RefreshVisual();
        }
    }
}