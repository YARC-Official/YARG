using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class SliderSettingVisual : AbstractSettingVisual<SliderSetting> {
		[SerializeField]
		private Slider slider;
		[SerializeField]
		private TMP_InputField inputField;

		// Unity sucks -_-
		private bool ignoreCallback = false;

		protected override void OnSettingInit() {
			ignoreCallback = true;

			slider.minValue = Setting.Min;
			slider.maxValue = Setting.Max;

			ignoreCallback = false;

			RefreshVisual();
		}

		protected override void RefreshVisual() {
			slider.SetValueWithoutNotify(Setting.Data);
			inputField.text = Setting.Data.ToString("N2", CultureInfo.InvariantCulture);
		}

		public void OnSliderChange() {
			if (ignoreCallback) {
				return;
			}

			Setting.Data = slider.value;
			RefreshVisual();
		}

		public void OnTextChange() {
			string text = inputField.text;

			try {
				Setting.Data = float.Parse(text, CultureInfo.InvariantCulture);
			} catch { }

			RefreshVisual();
		}
	}
}