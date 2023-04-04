using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class VolumeSetting : AbstractSetting {
		[SerializeField]
		private TMP_InputField textField;
		[SerializeField]
		private Slider slider;

		protected override void OnSetup() {
			base.OnSetup();

			UpdateTextAndSlider();
		}

		private void Update() {
			bool isInteractable = IsInteractable;

			textField.interactable = isInteractable;
			slider.interactable = isInteractable;
		}

		private void UpdateTextAndSlider() {
			float number = SettingsManager.GetSettingValue<float>(settingName);
			textField.text = (number * 100f).ToString("N1", CultureInfo.InvariantCulture) + "%";
			slider.value = number;
		}

		public void OnTextUpdate() {
			string text = textField.text;
			if (text.EndsWith("%")) {
				text = text[..^1];
			}
			float number = float.Parse(text, CultureInfo.InvariantCulture) / 100f;

			SettingsManager.SetSettingValue(settingName, number);
			UpdateTextAndSlider();
		}

		public void OnSliderUpdate() {
			float number = slider.value;
			SettingsManager.SetSettingValue(settingName, number);
			UpdateTextAndSlider();
		}
	}
}