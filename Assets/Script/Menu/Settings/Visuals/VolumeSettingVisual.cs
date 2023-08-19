using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class VolumeSettingVisual : BaseSettingVisual<VolumeSetting>
    {
        [SerializeField]
        private Slider _slider;

        [SerializeField]
        private TMP_InputField _inputField;

        protected override void OnSettingInit()
        {
            RefreshVisual();
        }

        public override void RefreshVisual()
        {
            _slider.SetValueWithoutNotify(Setting.Data);
            _inputField.text = (Setting.Data * 100f).ToString("N1", CultureInfo.InvariantCulture) + "%";
        }

        public void OnSliderChange()
        {
            Setting.Data = _slider.value;
            RefreshVisual();
        }

        public void OnTextChange()
        {
            string text = _inputField.text;
            if (text.EndsWith("%"))
            {
                text = text[..^1];
            }

            try
            {
                float number = float.Parse(text, CultureInfo.InvariantCulture) / 100f;

                _slider.value = number;
            }
            catch
            {
            }

            RefreshVisual();
        }
    }
}