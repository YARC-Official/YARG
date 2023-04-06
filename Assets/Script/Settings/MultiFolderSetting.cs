using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings {
	public sealed class MultiFolderSetting : AbstractSetting {
		[SerializeField]
		private TextMeshProUGUI infoText;
		[SerializeField]
		private Button addFolderButton;

		[Space]
		[SerializeField]
		private GameObject folderEntryPrefab;

		private List<FolderEntry> folderEntries = new();

		protected override void OnSetup() {
			base.OnSetup();

			ReloadFolderEntries();
		}

		private void Update() {
			bool isInteractable = IsInteractable;

			foreach (var folderEntry in folderEntries) {
				folderEntry.SetInteractable(isInteractable);
			}
		}

		public void ReloadFolderEntries() {
			foreach (var folderEntry in folderEntries) {
				Destroy(folderEntry.gameObject);
			}

			folderEntries.Clear();

			var folders = SettingsManager.GetSettingValue<string[]>(settingName);
			for (int i = 0; i < folders.Length; i++) {
				var folder = folders[i];

				var obj = Instantiate(folderEntryPrefab, transform);
				var folderEntry = obj.GetComponent<FolderEntry>();

				folderEntry.Setup(settingName, i, this);

				folderEntries.Add(folderEntry);
			}

			infoText.text = $"{folders.Length} folder(s) loaded.";
			settingsMenu.ForceUpdateLayout();
		}

		public void AddFolder() {
			var folders = SettingsManager.GetSettingValue<string[]>(settingName);
			var newFolders = new string[folders.Length + 1];
			folders.CopyTo(newFolders, 0);

			SettingsManager.SetSettingValue(settingName, newFolders, true);
			ReloadFolderEntries();
		}
	}
}