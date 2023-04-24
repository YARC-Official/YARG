using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace YARG.Settings {
	public class SettingsTab : MonoBehaviour {
		[SerializeField]
		private Button button;
		[SerializeField]
		private LocalizeStringEvent text;

		private string tabName;

		public void SetTab(string tabName) {
			this.tabName = tabName;

			text.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = $"Tab.{tabName}"
			};
		}

		private void Update() {
			if (tabName == null) {
				return;
			}

			button.interactable = GameManager.Instance.SettingsMenu.CurrentTab != tabName;
		}

		public void OnTabClick() {
			GameManager.Instance.SettingsMenu.CurrentTab = tabName;
		}
	}
}