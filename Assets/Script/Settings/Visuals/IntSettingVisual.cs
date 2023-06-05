using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class IntSettingVisual : AbstractSettingVisual<IntSetting> {
		[SerializeField]
		private TMP_InputField inputField;

		protected override void OnSettingInit() {
			RefreshVisual();
		}

		public override void RefreshVisual() {
			inputField.text = Setting.Data.ToString(CultureInfo.InvariantCulture);
		}

		public void OnTextFieldChange() {
			try {
				int value = int.Parse(inputField.text, CultureInfo.InvariantCulture);
				value = Mathf.Clamp(value, Setting.Min, Setting.Max);

				Setting.Data = value;
			} catch { }

			RefreshVisual();
		}
	}
}