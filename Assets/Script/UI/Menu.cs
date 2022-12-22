using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IniParser;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.UI {
	public class Menu : MonoBehaviour {
		public static Menu Instance {
			get;
			private set;
		}

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
			Instance = this;

			if (songs == null) {
				if (Game.CACHE_FILE.Exists || PlayerManager.client != null) {
					await Task.Run(() => FetchSongsFromCache());
				} else {
					FetchSongs();
					await Task.Run(() => FetchSongInfo());
				}
			}

			songs = songs.OrderBy(song => song.SongNameNoParen).ToList();

			// Spawn song infos
			songInfoComponents = new();
			char currentSection = ' ';
			foreach (var song in songs) {
				// Skip errored songs
				if (song.errored) {
					continue;
				}

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

		private static void FetchSongs() {
			var songFolder = Game.SONG_FOLDER;
			var directories = songFolder.GetDirectories();

			songs = new(directories.Length);
			foreach (var folder in directories) {
				songs.Add(new SongInfo(folder));
			}
		}

		private static void FetchSongInfo() {
			// Fetch song info manually
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
				} catch (Exception e) {
					song.errored = true;
					Debug.LogException(e);
				}
			}

			// Create cache
			var json = JsonConvert.SerializeObject(songs, Formatting.Indented);
			File.WriteAllText(Game.CACHE_FILE.ToString(), json.ToString());
		}

		private static void FetchSongsFromCache() {
			var cacheFile = Game.CACHE_FILE;
			if (PlayerManager.client != null) {
				cacheFile = PlayerManager.client.remoteCache;
			}

			string json = File.ReadAllText(cacheFile.ToString());
			songs = JsonConvert.DeserializeObject<List<SongInfo>>(json);
		}

		private static char SongNameToLetterSection(string nameNoParen) {
			char o = nameNoParen.ToUpper()[0];
			if (char.IsNumber(o)) {
				o = '#';
			}

			return o;
		}

		public static void DownloadSong(SongInfo songInfo) {
			PlayerManager.client.RequestDownload(songInfo.folder.FullName);
		}
	}
}