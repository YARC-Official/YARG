using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Settings.SettingTypes;

namespace YARG.Settings.SettingVisuals {
	public class IntSettingVisual : AbstractSettingVisual<IntSetting> {
		[SerializeField]
		private TMP_InputField inputField;

		protected override void OnSettingInit() {
			inputField.text = Setting.Data.ToString(CultureInfo.InvariantCulture);
		}

		protected override void OnSettingChange() {
			int value = int.Parse(inputField.text, CultureInfo.InvariantCulture);
			value = Mathf.Clamp(value, Setting.Min, Setting.Max);

			Setting.Data = value;
			inputField.text = value.ToString(CultureInfo.InvariantCulture);
		}

		public void OnTextFieldChange() {
			OnSettingChange();
		}
	}
}