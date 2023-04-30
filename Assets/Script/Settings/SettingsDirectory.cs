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

		private int index;

		public void SetIndex(int index) {
			this.index = index;

			RefreshText();
		}

		private void RefreshText() {
			if (string.IsNullOrEmpty(SongLibrary.SongFolders[index])) {
				pathText.text = "<i>No Folder</i>";
				songCountText.text = "";
			} else {
				pathText.text = SongLibrary.SongFolders[index];

				int songCount = SongLibrary.Songs.Count(i =>
					Utils.PathsEqual(i.cacheRoot, SongLibrary.SongFolders[index]));
				songCountText.text = $"{songCount} <alpha=#60>SONGS";
			}
		}

		public void Remove() {
			// Remove the element
			var list = SongLibrary.SongFolders.ToList();
			list.RemoveAt(index);
			SongLibrary.SongFolders = list.ToArray();

			// Refresh
			GameManager.Instance.SettingsMenu.UpdateSongFolderManager();
		}

		public void Browse() {
			var startingDir = SongLibrary.SongFolders[index];
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, folder => {
				if (folder == null || folder.Length == 0) {
					return;
				}

				SongLibrary.SongFolders[index] = folder[0];
				RefreshText();
			});
		}

		public void Refresh() {
			// Delete it
			var file = SongLibrary.HashFilePath(SongLibrary.SongFolders[index]);
			var path = Path.Combine(SongLibrary.CacheFolder, file + ".json");
			if (File.Exists(path)) {
				File.Delete(path);
			}

			// Refresh
			MainMenu.Instance.RefreshSongLibrary();
		}
	}
}