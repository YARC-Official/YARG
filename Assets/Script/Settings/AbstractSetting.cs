using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace YARG.Settings {
	public abstract class AbstractSetting : MonoBehaviour {
		[SerializeField]
		private LocalizeStringEvent settingText;

		protected string settingName;

		private System.Func<bool> interactableFunc;

		/// <returns>
		/// Whether the setting is able to be modified.
		/// </returns>
		public bool IsInteractable => interactableFunc == null || interactableFunc();

		public void Setup(string settingName) {
			settingText.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = settingName
			};
			this.settingName = settingName;

			interactableFunc = SettingsManager.GetSettingInfo(settingName).isInteractable;

			OnSetup();
		}

		protected virtual void OnSetup() {

		}
	}
}