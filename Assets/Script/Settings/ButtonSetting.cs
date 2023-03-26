using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class ButtonSetting : AbstractSetting {
		[SerializeField]
		private Button button;

		public void OnButtonPress() {
			SettingsManager.InvokeButtonAction(settingName);
		}

		private void Update() {
			button.interactable = IsInteractable;
		}
	}
}