using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class ButtonSetting : AbstractSetting {
		[SerializeField]
		private Button button;

		public void OnButtonPress() {
			SettingsManager.GetSettingInfo(settingName).buttonAction?.Invoke();
		}

		private void Update() {
			button.interactable = IsInteractable;
		}
	}
}