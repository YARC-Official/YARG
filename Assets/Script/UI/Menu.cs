using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IniParser;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YARG.UI {
	public class Menu : MonoBehaviour {
		private static Dictionary<string, SongInfo> songs;

		[SerializeField]
		private GameObject songViewPrefab;

		[SerializeField]
		private Transform songListContent;

		private List<SongInfoComponent> songInfoComponents;

		private async void Start() {
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

			// Select the first song by default
			songInfoComponents[0].GetComponent<Button>().Select();

			await Task.Run(() => FetchSongInfo());
			UpdateAll();
		}

		private void UpdateAll() {
			foreach (var songComp in songInfoComponents) {
				songComp.UpdateText();
			}
		}

		private void Update() {
			if (Keyboard.current.rKey.wasPressedThisFrame) {
				UpdateAll();
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

		private static void FetchSongInfo() {
			var parser = new FileIniDataParser();

			foreach (var kv in songs) {
				if (kv.Value.fetched) {
					return;
				}

				var file = new FileInfo(Path.Combine(kv.Value.folder.ToString(), "song.ini"));
				if (!file.Exists) {
					return;
				}

				kv.Value.fetched = true;
				try {
					var data = parser.ReadFile(file.FullName);

					// Set basic info
					kv.Value.songName ??= data["song"]["name"];
					kv.Value.artistName ??= data["song"]["artist"];

					// Get song length
					int rawLength = int.Parse(data["song"]["song_length"]);
					kv.Value.songLength = rawLength / 1000f;
				} catch {
					kv.Value.errored = true;
				}
			}
		}
	}
}