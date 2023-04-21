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

		// Creates .TXT file witth current song information
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
			// Open the text file for appending
			using var writer = new StreamWriter(TextFilePath, false);

			// Make the file blank (Avoid errors in OBS)
			writer.Write("");
		}

		private void DeleteCurrentSongFile() {
			// Open the text file for appending
			using var writer = new StreamWriter(TextFilePath, false);

			// Make the file blank (Avoid errors in OBS)
			writer.Write("");
		}

		private void OnApplicationQuit() {
			DeleteCurrentSongFile();
		}

		void OnSongStart(SongInfo song) {
			// Open the text file for appending
			using var writer = new StreamWriter(TextFilePath, false);

			// Write two lines of text to the file
			writer.Write($"{song.SongName}\n{song.artistName}\n{song.album}\n{song.genre}\n{song.year}\n{song.SourceFriendlyName}\n{song.charter}");
		}

		void OnSongEnd(SongInfo song) {
			// Open the text file for appending
			using var writer = new StreamWriter(TextFilePath, false);

			// Make the file blank (Avoid errors in OBS)
			writer.Write("");
		}

		private void OnInstrumentSelection(PlayerManager.Player playerInfo) {
			// Selecting Instrument
		}

		private void OnPauseToggle(bool pause) {
			// Game Paused
		}
	}
}
