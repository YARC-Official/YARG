using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class ToggleSetting : AbstractSetting {
		[SerializeField]
		private Toggle toggle;

		protected override void OnSetup() {
			base.OnSetup();

			UpdateToggle();
		}

		private void Update() {
			toggle.interactable = IsInteractable;
		}

		private void UpdateToggle() {
			toggle.isOn = SettingsManager.GetSettingValue<bool>(settingName);
		}

		public void OnToggleUpdate() {
			SettingsManager.SetSettingValue(settingName, toggle.isOn);
			UpdateToggle();
		}
	}
}