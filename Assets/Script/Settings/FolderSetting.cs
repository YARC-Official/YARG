using System.IO;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class FolderSetting : AbstractSetting {
		[SerializeField]
		private TMP_InputField textField;
		[SerializeField]
		private Button browseButton;

		protected override void OnSetup() {
			base.OnSetup();

			UpdateText();
		}

		private void Update() {
			bool isInteractable = IsInteractable;

			textField.interactable = isInteractable;
			browseButton.interactable = isInteractable;
		}

		private void UpdateText() {
			textField.text = SettingsManager.GetSettingValue<string>(settingName);
		}

		public void OnTextUpdate() {
			if (!Directory.Exists(textField.text)) {
				UpdateText();
				return;
			}

			SettingsManager.SetSettingValue(settingName, textField.text);
			UpdateText();
		}

		public void BrowseSongFolder() {
			var startingDir = SettingsManager.GetSettingValue<string>(settingName);
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, folder => {
				if (folder.Length == 0) {
					return;
				}

				SettingsManager.SetSettingValue(settingName, folder[0]);
				UpdateText();
			});
		}
	}
}