using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace YARG.Settings {
	public class SettingsButton : MonoBehaviour {
		private string buttonName;

		[SerializeField]
		private LocalizeStringEvent text;

		public void SetInfo(string buttonName) {
			this.buttonName = buttonName;

			text.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = buttonName[1..]
			};
		}

		public void OnClick() {
			SettingsManager.InvokeButton(buttonName);
		}
	}
}