using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace YARG.Settings {
	public class SettingsTab : MonoBehaviour {
		[SerializeField]
		private Image icon;
		[SerializeField]
		private Button button;
		[SerializeField]
		private LocalizeStringEvent text;

		private string tabName;

		public void SetTab(string tabName, string iconName) {
			this.tabName = tabName;

			// Set icon
			icon.sprite = Addressables.LoadAssetAsync<Sprite>($"SettingIcons[{iconName}]").WaitForCompletion();

			// Set text
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