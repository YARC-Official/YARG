using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class VolumeSettingVisual : AbstractSettingVisual<VolumeSetting> {
		[SerializeField]
		private Slider slider;
		[SerializeField]
		private TMP_InputField inputField;

		protected override void OnSettingInit() {
			RefreshVisual();
		}

		protected override void RefreshVisual() {
			slider.SetValueWithoutNotify(Setting.Data);
			inputField.text = (Setting.Data * 100f).ToString("N1", CultureInfo.InvariantCulture) + "%";
		}

		public void OnSliderChange() {
			Setting.Data = slider.value;
			RefreshVisual();
		}

		public void OnTextChange() {
			string text = inputField.text;
			if (text.EndsWith("%")) {
				text = text[..^1];
			}

			try {
				float number = float.Parse(text, CultureInfo.InvariantCulture) / 100f;

				slider.value = number;

			} catch { }

			RefreshVisual();
		}
	}
}