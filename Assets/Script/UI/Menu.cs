using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YARG.UI {
	public class Menu : MonoBehaviour {
		private static Dictionary<string, SongInfo> songs;

		[SerializeField]
		private GameObject songViewPrefab;

		[SerializeField]
		private Transform songListContent;

		private List<SongInfoComponent> songInfoComponents;

		private void Start() {
			if (songs == null) {
				FetchSongs();
			}

			songInfoComponents = new();
			foreach (var song in songs) {
				var songView = Instantiate(songViewPrefab, songListContent);

				var songComp = songView.GetComponentInChildren<SongInfoComponent>();
				songComp.songInfo = song.Value;
				songComp.UpdateText();

				songInfoComponents.Add(songComp);
			}
		}

		private void UpdateAll() {
			foreach (var songComp in songInfoComponents) {
				songComp.UpdateText();
			}
		}

		private static void FetchSongs() {
			var songFolder = new DirectoryInfo(@"B:\Clone Hero Alpha\Songs");
			var directories = songFolder.GetDirectories();

			songs = new(directories.Length);
			foreach (var folder in directories) {
				songs.Add(folder.Name, new SongInfo(folder));
			}
		}
	}
}