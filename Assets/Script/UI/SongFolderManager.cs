using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class SongFolderManager : MonoBehaviour {
		public static SongFolderManager Instance {
			get;
			private set;
		}

		[SerializeField]
		private TextMeshProUGUI infoText;
		[SerializeField]
		private Transform folderList;

		[Space]
		[SerializeField]
		public GameObject folderEntryPrefab;

		private void OnEnable() {
			Instance = this;

			UpdateInfo();
		}

		private void OnDisable() {
			// Save on close
			Settings.SettingsManager.SaveSettings();

			// Refresh song library
			MainMenu.Instance.RefreshSongLibrary();
		}

		private void UpdateInfo() {
			infoText.text = $"{SongLibrary.Songs.Count} song(s) loaded, {SongLibrary.SongFolders.Length} folder(s).";

			// Clear folder list
			foreach (Transform child in folderList) {
				Destroy(child.gameObject);
			}

			// Spawn folder entries
			for (int i = 0; i < SongLibrary.SongFolders.Length; i++) {
				var folderEntry = Instantiate(folderEntryPrefab, folderList);
				folderEntry.GetComponent<FolderEntry>().SetInfo(i, SongLibrary.SongFolders[i]);
			}
		}

		public void RefreshAllCaches() {
			if (Directory.Exists(SongLibrary.CacheFolder)) {
				Directory.Delete(SongLibrary.CacheFolder, true);
				MainMenu.Instance.RefreshSongLibrary();
			}
		}

		public void AddFolder() {
			// Use a list to add the new folder to the end
			var newFolders = new List<string>(SongLibrary.SongFolders) {
				""
			};

			// Convert to array and save
			SongLibrary.SongFolders = newFolders.ToArray();
			UpdateInfo();
		}

		public void RemoveFolder(int i) {
			// Use a list to remove the folder
			var newFolders = new List<string>(SongLibrary.SongFolders);
			newFolders.RemoveAt(i);

			// Convert to array and save
			SongLibrary.SongFolders = newFolders.ToArray();
			UpdateInfo();
		}
	}
}