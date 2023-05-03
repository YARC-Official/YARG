using System.IO;
using System.Linq;
using SFB;
using TMPro;
using UnityEngine;
using YARG.UI;
using YARG.Util;

namespace YARG.Settings {
	public class SettingsDirectory : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI pathText;
		[SerializeField]
		private TextMeshProUGUI songCountText;

		private bool isUpgradeFolder;
		private int index;

		private string[] _pathsReference;
		private string[] PathsReference {
			get => _pathsReference;
			set {
				if (isUpgradeFolder) {
					SongLibrary.SongUpgradeFolders = value;
				} else {
					SongLibrary.SongFolders = value;
				}
			}
		}

		public void SetIndex(int index, bool isUpgradeFolder) {
			this.index = index;
			this.isUpgradeFolder = isUpgradeFolder;

			if (isUpgradeFolder) {
				_pathsReference = SongLibrary.SongUpgradeFolders;
			} else {
				_pathsReference = SongLibrary.SongFolders;
			}

			RefreshText();
		}

		private void RefreshText() {
			if (string.IsNullOrEmpty(PathsReference[index])) {
				pathText.text = "<i>No Folder</i>";
				songCountText.text = "";
			} else {
				pathText.text = PathsReference[index];

				if (isUpgradeFolder) {
					songCountText.text = "";
				} else {
					int songCount = SongLibrary.Songs.Count(i =>
						Utils.PathsEqual(i.cacheRoot, PathsReference[index]));
					songCountText.text = $"{songCount} <alpha=#60>SONGS";
				}
			}
		}

		public void Remove() {
			// Remove the element
			var list = PathsReference.ToList();
			list.RemoveAt(index);
			PathsReference = list.ToArray();

			// Refresh
			GameManager.Instance.SettingsMenu.UpdateSongFolderManager();
		}

		public void Browse() {
			var startingDir = PathsReference[index];
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, folder => {
				if (folder == null || folder.Length == 0) {
					return;
				}

				PathsReference[index] = folder[0];
				RefreshText();
			});
		}

		public void Refresh() {
			GameManager.Instance.SettingsMenu.hasSongLibraryChanged = false;

			// Delete it
			var file = SongLibrary.HashFilePath(PathsReference[index]);
			var path = Path.Combine(SongLibrary.CacheFolder, file + ".json");
			if (File.Exists(path)) {
				File.Delete(path);
			}

			// Refresh
			MainMenu.Instance.RefreshSongLibrary();
		}
	}
}