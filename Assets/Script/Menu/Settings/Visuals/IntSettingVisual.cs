using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class IntSettingVisual : BaseSettingVisual<IntSetting>
    {
        [SerializeField]
        private TMP_InputField _inputField;

        protected override void OnSettingInit()
        {
            RefreshVisual();
        }

        public override void RefreshVisual()
        {
            _inputField.text = Setting.Data.ToString(CultureInfo.InvariantCulture);
        }

        public void OnTextFieldChange()
        {
            try
            {
                int value = int.Parse(_inputField.text, CultureInfo.InvariantCulture);
                value = Mathf.Clamp(value, Setting.Min, Setting.Max);

                Setting.Data = value;
            }
            catch
            {
            }

            RefreshVisual();
        }
    }
}