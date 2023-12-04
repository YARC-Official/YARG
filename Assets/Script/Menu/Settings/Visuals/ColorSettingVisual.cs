using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class ColorSettingVisual : BaseSettingVisual<ColorSetting>
    {
        [SerializeField]
        private Image _colorRectangle;

        [SerializeField]
        private TMP_InputField _inputField;
        [SerializeField]
        private TMP_InputField _opacityField;

        protected override void RefreshVisual()
        {
            _colorRectangle.color = Setting.Value;
            _inputField.text = ColorUtility.ToHtmlStringRGB(Setting.Value);

            if (Setting.AllowTransparency)
            {
                _opacityField.gameObject.SetActive(true);
                _opacityField.text = (Setting.Value.a * 100f)
                    .ToString("N0", CultureInfo.InvariantCulture);
            }
            else
            {
                _opacityField.gameObject.SetActive(false);
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
            // Unity needs a hashtag here, but doesn't put it in when converting to string
            if (ColorUtility.TryParseHtmlString("#" + _inputField.text, out var color))
            {
                color.a = Setting.Value.a;
                Setting.Value = color;
            }

            RefreshVisual();
        }

        public void OnOpacityFieldChange()
        {
            try
            {
                float value = float.Parse(_opacityField.text, CultureInfo.InvariantCulture) / 100f;
                value = Mathf.Clamp01(value);

                var c = Setting.Value;
                c.a = value;
                Setting.Value = c;
            }
            catch
            {
                // Ignore error
            }

            RefreshVisual();
        }

        public void OpenColorPicker()
        {
            DialogManager.Instance.ShowColorPickerDialog(Setting.Value, color =>
            {
                Setting.Value = color;
                RefreshVisual();
            });
        }
    }
}