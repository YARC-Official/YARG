using System.IO;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;
using YARG.UI;

namespace YARG {
	public class TwitchController : MonoBehaviour {
		public static TwitchController Instance {
			get;
			private set;
		}

		public string TextFilePath => Path.Combine(GameManager.PersistentDataPath, "currentSong.txt");

		private void Start() {
			Instance = this;

			// While YARG should delete the file on exit, you never know if a crash or something prevented that.
			DeleteCurrentSongFile();
			CreateEmptySongFile();

			// Listen to the changing of songs
			Play.OnSongStart += OnSongStart;
			Play.OnSongEnd += OnSongEnd;

			// Listen to instrument selection - NYI, let's confirm the rest works
			DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;

			// Listen to pausing - NYI, let's confirm the rest works
			Play.OnPauseToggle += OnPauseToggle;
		}

		private void CreateEmptySongFile() {
			File.Create(TextFilePath).Dispose();
		}

		private void DeleteCurrentSongFile() {
			if (File.Exists(TextFilePath)) {
				File.Delete(TextFilePath);
			}
		}

		private void OnApplicationQuit() {
			DeleteCurrentSongFile();
		}

		void OnSongStart(SongInfo song) {
			// Open the text file for appending
			using var writer = new StreamWriter(TextFilePath, false);

			// Write two lines of text to the file
			writer.Write($"{song.SongName}\n{song.artistName}");
		}

		void OnSongEnd(SongInfo song) {
			// When the song ends, empty the file
			DeleteCurrentSongFile();
			CreateEmptySongFile();
		}

		private void OnInstrumentSelection(PlayerManager.Player playerInfo) {
			// TODO
		}

		private void OnPauseToggle(bool pause) {
			// TODO
		}
	}
}