using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.SettingTypes;

namespace YARG.Settings.SettingVisuals {
	public class VolumeSettingVisual : AbstractSettingVisual<VolumeSetting> {
		[SerializeField]
		private Slider slider;
		[SerializeField]
		private TMP_InputField inputField;

		protected override void OnSettingInit() {
			slider.value = Setting.Data;
			UpdateTextInputField();
		}

		protected override void OnSettingChange() {
			Setting.Data = slider.value;
			UpdateTextInputField();
		}

		private void UpdateTextInputField() {
			inputField.text = (Setting.Data * 100f).ToString("N1", CultureInfo.InvariantCulture) + "%";
		}

		public void OnSliderChange() {
			OnSettingChange();
		}

		public void OnTextChange() {
			string text = inputField.text;
			if (text.EndsWith("%")) {
				text = text[..^1];
			}

			float number = float.Parse(text, CultureInfo.InvariantCulture) / 100f;

			slider.value = number;
			OnSettingChange();
		}
	}
}