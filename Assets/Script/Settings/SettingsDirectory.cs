using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using SFB;
using TMPro;
using UnityEngine;
using YARG.Song;
using YARG.Util;

namespace YARG.Settings {
	public class SettingsDirectory : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI pathText;
		[SerializeField]
		private TextMeshProUGUI songCountText;

		private bool isUpgradeFolder;
		private int index;

		private List<string> _pathsReference;
		private List<string> PathsReference {
			get => _pathsReference;
			set {
				if (isUpgradeFolder) {
					SettingsManager.Settings.SongUpgradeFolders = value;
				} else {
					SettingsManager.Settings.SongFolders = value;
				}
			}
		}

		public void SetIndex(int index, bool isUpgradeFolder) {
			this.index = index;
			this.isUpgradeFolder = isUpgradeFolder;

			if (isUpgradeFolder) {
				_pathsReference = SettingsManager.Settings.SongUpgradeFolders;
			} else {
				_pathsReference = SettingsManager.Settings.SongFolders;
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
					int songCount = SongContainer.Songs.Count(i =>
						Utils.PathsEqual(i.CacheRoot, PathsReference[index]));
					songCountText.text = $"{songCount} <alpha=#60>SONGS";
				}
			}
		}

		public void Remove() {
			// Remove the element
			PathsReference.RemoveAt(index);

			// Refresh
			GameManager.Instance.SettingsMenu.UpdateSongFolderManager();
		}

		public void Browse() {
			var startingDir = PathsReference[index];
			FileExplorerHelper.OpenChooseFolder(startingDir, folder => {
				PathsReference[index] = folder;
				RefreshText();
			});
		}

		public async void Refresh() {
			LoadingManager.Instance.QueueSongFolderRefresh(PathsReference[index]);
			await LoadingManager.Instance.StartLoad();
			RefreshText();
		}
	}
}