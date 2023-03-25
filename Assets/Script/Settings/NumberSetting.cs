using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class NumberSetting : AbstractSetting {
		[SerializeField]
		private TMP_InputField textField;
		[SerializeField]
		private Button addButton;
		[SerializeField]
		private Button subButton;

		protected override void OnSetup() {
			base.OnSetup();

			UpdateText();
		}

		private void Update() {
			bool isInteractable = IsInteractable;

			textField.interactable = isInteractable;
			addButton.interactable = isInteractable;
			subButton.interactable = isInteractable;
		}

		private void UpdateText() {
			int number = SettingsManager.GetSettingValue<int>(settingName);
			textField.text = number.ToString(CultureInfo.InvariantCulture);
		}

		public void OnTextUpdate() {
			int number = int.Parse(textField.text, CultureInfo.InvariantCulture);
			SettingsManager.SetSettingValue(settingName, number);
			UpdateText();
		}

		public void Add(int amount) {
			int number = SettingsManager.GetSettingValue<int>(settingName);
			number += amount;

			SettingsManager.SetSettingValue(settingName, number);
			UpdateText();
		}
	}
}