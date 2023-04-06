using System.IO;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public class FolderEntry : MonoBehaviour {
		[SerializeField]
		private TMP_InputField textField;
		[SerializeField]
		private Button removeButton;
		[SerializeField]
		private Button browseButton;

		private string settingName;
		private int folderIndex;

		private MultiFolderSetting multiFolderSetting;

		public void Setup(string settingName, int folderIndex, MultiFolderSetting multiFolderSetting) {
			this.multiFolderSetting = multiFolderSetting;
			this.settingName = settingName;
			this.folderIndex = folderIndex;

			UpdateText();
		}

		private void UpdateText() {
			var folders = SettingsManager.GetSettingValue<string[]>(settingName);
			textField.text = folders[folderIndex];
		}

		private void SetSettingValue(string value) {
			var folders = SettingsManager.GetSettingValue<string[]>(settingName);

			// Skip if nothing changed
			if (folders[folderIndex] == value) {
				return;
			}

			folders[folderIndex] = value;
			SettingsManager.InvokeSettingChangeAction(settingName);
		}

		public void OnTextUpdate() {
			if (!Directory.Exists(textField.text)) {
				UpdateText();
				return;
			}

			SetSettingValue(textField.text);
			UpdateText();
		}

		public void BrowseSongFolder() {
			var folders = SettingsManager.GetSettingValue<string[]>(settingName);

			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", folders[folderIndex], false, folder => {
				if (folder.Length == 0) {
					return;
				}

				SetSettingValue(folder[0]);
				UpdateText();
			});
		}

		public void SetInteractable(bool interactable) {
			textField.interactable = interactable;
			removeButton.interactable = interactable;
			browseButton.interactable = interactable;
		}

		public void RemoveFolderButton() {
			// Get the current folders
			var folders = SettingsManager.GetSettingValue<string[]>(settingName);

			// Remove the folder at the index
			var newFolders = new string[folders.Length - 1];
			for (int i = 0; i < folderIndex; i++) {
				newFolders[i] = folders[i];
			}
			for (int i = folderIndex + 1; i < folders.Length; i++) {
				newFolders[i - 1] = folders[i];
			}

			// Update the setting
			SettingsManager.SetSettingValue(settingName, newFolders);
			multiFolderSetting.ReloadFolderEntries();
		}
	}
}