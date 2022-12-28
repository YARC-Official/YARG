using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Data;

namespace YARG.UI {
	public class Menu : MonoBehaviour {
		public static Menu Instance {
			get;
			private set;
		}

		[SerializeField]
		private GameObject songViewPrefab;
		[SerializeField]
		private GameObject sectionHeaderPrefab;

		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private Toggle botModeToggle;

		private List<SongInfoComponent> songInfoComponents;

		private void Start() {
			Instance = this;

			SongLibrary.FetchSongs();

			var songs = SongLibrary.Songs
				.OrderBy(song => song.SongNameNoParen)
				.ToList();

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

		private static char SongNameToLetterSection(string nameNoParen) {
			char o = nameNoParen.ToUpper()[0];
			if (char.IsNumber(o)) {
				o = '#';
			}

			return o;
		}

		public static void DownloadSong(SongInfo songInfo) {
			GameManager.client.RequestDownload(songInfo.folder.FullName);
		}
	}
}