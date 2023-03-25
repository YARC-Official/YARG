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
			var dirInfo = SettingsManager.GetSettingValue<DirectoryInfo>(settingName);
			textField.text = dirInfo.FullName;
		}

		public void OnTextUpdate() {
			var newDir = new DirectoryInfo(textField.text);

			if (!newDir.Exists) {
				UpdateText();
				return;
			}

			SettingsManager.SetSettingValue(settingName, newDir);
			UpdateText();
		}

		public void BrowseSongFolder() {
			var startingDir = SettingsManager.GetSettingValue<DirectoryInfo>(settingName).FullName;
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, folder => {
				if (folder.Length == 0) {
					return;
				}

				SettingsManager.SetSettingValue(settingName, new DirectoryInfo(folder[0]));
				UpdateText();
			});
		}
	}
}