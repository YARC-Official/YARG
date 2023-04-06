using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace YARG.Settings {
	public abstract class AbstractSetting : MonoBehaviour {
		[SerializeField]
		private LocalizeStringEvent settingText;

		protected string settingName;
		protected SettingsMenu settingsMenu;

		/// <returns>
		/// Whether the setting is able to be modified.
		/// </returns>
		public bool IsInteractable => SettingsManager.IsSettingInteractable(settingName);

		public void Setup(string settingName, SettingsMenu settingsMenu) {
			settingText.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = settingName
			};
			this.settingName = settingName;

			this.settingsMenu = settingsMenu;

			OnSetup();
		}

		protected virtual void OnSetup() {

		}
	}
}