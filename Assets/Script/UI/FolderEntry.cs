using System.IO;
using Cysharp.Threading.Tasks;
using SFB;
using TMPro;
using UnityEngine;
using YARG.Util;

namespace YARG.UI {
	public class FolderEntry : MonoBehaviour {
		[SerializeField]
		private TMP_InputField folderPath;

		private int index;

		public void SetInfo(int index, string path) {
			this.index = index;
			folderPath.text = path;
		}

		private void SetFolderPath(string path) {
			SongLibrary.SongFolders[index] = path;
			folderPath.text = path;
		}

		public void OnTextUpdate() {
			SetFolderPath(folderPath.text);
		}

		public void BrowseSongFolder() {
			var startingDir = SongLibrary.SongFolders[index];
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, folder => {
				if (folder.Length == 0) {
					return;
				}

				SetFolderPath(folder[0]);
			});
		}

		public void RemoveFolder() {
			SongFolderManager.Instance.RemoveFolder(index);
		}

		public async UniTask RefreshThisFolder() {
			// Delete it
			var file = SongLibrary.HashFilePath(SongLibrary.SongFolders[index]);
			var path = Path.Combine(SongLibrary.CacheFolder, file + ".json");
			if (File.Exists(path)) {
				File.Delete(path);
			}

			// Refresh it
			await MainMenu.Instance.RefreshSongLibrary();
		}
	}
}