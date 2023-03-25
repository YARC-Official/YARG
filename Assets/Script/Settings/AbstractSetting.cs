using TMPro;
using UnityEngine;

namespace YARG.Settings {
	public abstract class AbstractSetting : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI settingText;

		protected string settingName;

		private System.Func<bool> interactableFunc;

		/// <returns>
		/// Whether the setting is able to be modified.
		/// </returns>
		public bool IsInteractable => interactableFunc == null || interactableFunc();

		public void Setup(string text, string settingName) {
			settingText.text = text;
			this.settingName = settingName;

			interactableFunc = SettingsManager.GetSettingInfo(settingName).isInteractable;

			OnSetup();
		}

		protected virtual void OnSetup() {

		}
	}
}