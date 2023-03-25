using TMPro;
using UnityEngine;

namespace YARG.Settings {
	public sealed class TextSetting : AbstractSetting {
		[SerializeField]
		private TMP_InputField textField;

		protected override void OnSetup() {
			base.OnSetup();

			textField.text = SettingsManager.GetSettingValue<string>(settingName);
		}

		private void Update() {
			textField.interactable = IsInteractable;
		}

		public void OnTextUpdate() {
			SettingsManager.SetSettingValue(settingName, textField.text);
		}
	}
}