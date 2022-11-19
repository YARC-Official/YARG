using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IniParser;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YARG.UI {
	public class Menu : MonoBehaviour {
		private static List<SongInfo> songs;

		[SerializeField]
		private GameObject songViewPrefab;
		[SerializeField]
		private GameObject sectionHeaderPrefab;

		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private Toggle botModeToggle;

		private List<SongInfoComponent> songInfoComponents;

		private async void Start() {
			if (songs == null) {
				FetchSongs();
			}

			await Task.Run(() => FetchSongInfo());
			songs = songs.OrderBy(song => song.SongNameNoParen).ToList();

			songInfoComponents = new();
			char currentSection = ' ';
			foreach (var song in songs) {
				char section = SongNameToLetterSection(song.SongNameNoParen);
				if (section != currentSection) {
					currentSection = section;

					var sectionHeader = Instantiate(sectionHeaderPrefab, songListContent);
					sectionHeader.GetComponentInChildren<TextMeshProUGUI>().text = section.ToString();
				}

				var songView = Instantiate(songViewPrefab, songListContent);

				var songComp = songView.GetComponentInChildren<SongInfoComponent>();
				songComp.songInfo = song;
				songComp.UpdateText();

				songInfoComponents.Add(songComp);
			}

			// Select the first song by default
			songInfoComponents[0].GetComponent<Button>().Select();
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

		public void UpdateBotMode() {
			Game.botMode = botModeToggle.isOn;
		}

		private static void FetchSongs() {
			var songFolder = new DirectoryInfo(@"B:\Clone Hero Alpha\Songs");
			var directories = songFolder.GetDirectories();

			songs = new(directories.Length);
			foreach (var folder in directories) {
				songs.Add(new SongInfo(folder));
			}
		}

		private static void FetchSongInfo() {
			var parser = new FileIniDataParser();

			foreach (var song in songs) {
				if (song.fetched) {
					return;
				}

				var file = new FileInfo(Path.Combine(song.folder.ToString(), "song.ini"));
				if (!file.Exists) {
					return;
				}

				song.fetched = true;
				try {
					var data = parser.ReadFile(file.FullName);

					// Set basic info
					song.SongName ??= data["song"]["name"];
					song.artistName ??= data["song"]["artist"];

					// Get song length
					int rawLength = int.Parse(data["song"]["song_length"]);
					song.songLength = rawLength / 1000f;
				} catch {
					song.errored = true;
				}

				break;
			}
		}

		private static char SongNameToLetterSection(string nameNoParen) {
			char o = nameNoParen.ToUpper()[0];
			if (char.IsNumber(o)) {
				o = '#';
			}

			return o;
		}
	}
}