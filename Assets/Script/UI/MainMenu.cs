using System;
using System.Collections.Generic;
using System.IO;
using SFB;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Data;
using YARG.PlayMode;
using YARG.Util;

namespace YARG.UI {
	public partial class MainMenu : MonoBehaviour {
		public static bool postSong = false;

		public static MainMenu Instance {
			get;
			private set;
		}

		public SongInfo chosenSong = null;

		[SerializeField]
		private UIDocument editPlayersDocument;
		[SerializeField]
		private UIDocument postSongDocument;

		[SerializeField]
		private Canvas mainMenu;
		[SerializeField]
		private Canvas songSelect;
		[SerializeField]
		private Canvas difficultySelect;

		[SerializeField]
		private GameObject settingsMenu;

		private void Start() {
			Instance = this;

			// Load song folder from player prefs
			if (!string.IsNullOrEmpty(PlayerPrefs.GetString("songFolder"))) {
				SongLibrary.songFolder = new(PlayerPrefs.GetString("songFolder"));
			}

			if (!postSong) {
				ShowMainMenu();
			} else {
				ShowPostSong();
			}
		}

		private void OnDisable() {
			// Save player prefs
			PlayerPrefs.Save();
		}

		private void SetupPostSong() {
			var root = postSongDocument.rootVisualElement;

			// Create a score to push

			var songScore = new SongScore {
				lastPlayed = DateTime.Now,
				timesPlayed = 1,
				highestPercent = new()
			};
			var oldScore = ScoreManager.GetScore(Play.song);

			HashSet<PlayerManager.Player> highScores = new();
			foreach (var player in PlayerManager.players) {
				if (Play.speed != 1f) {
					continue;
				}

				if (player.inputStrategy.botMode) {
					continue;
				}

				if (!player.lastScore.HasValue) {
					continue;
				}

				var lastScore = player.lastScore.GetValueOrDefault();

				// Skip if the chart has no notes (will be viewed as 100%)
				if (lastScore.notesHit == 0) {
					continue;
				}

				// Override or add percentage
				if (oldScore == null ||
					!oldScore.highestPercent.TryGetValue(player.chosenInstrument, out var oldHighest) ||
					lastScore.percentage > oldHighest) {

					songScore.highestPercent[player.chosenInstrument] = lastScore.percentage;
					highScores.Add(player);
				}
			}

			// Push!
			ScoreManager.PushScore(Play.song, songScore);

			// Setup score label

			var label = root.Q<Label>("Score");
			label.text = "";

			foreach (var player in PlayerManager.players) {
				if (!player.lastScore.HasValue) {
					continue;
				}

				var score = player.lastScore.Value;
				label.text += $"{player.DisplayName}: {score.percentage.percent * 100f:N1}%, {score.notesHit} hit, {score.notesMissed} missed";

				if (highScores.Contains(player)) {
					label.text += " <color=green>HIGH SCORE!</color>";
				}

				label.text += "\n\n";
			}

			// Next button

			var nextButton = root.Q<Button>("NextButton");
			nextButton.clicked += () => {
				ShowMainMenu();
			};
		}

		private void HideAll() {
			editPlayersDocument.SetVisible(false);
			postSongDocument.SetVisible(false);

			mainMenu.gameObject.SetActive(false);
			songSelect.gameObject.SetActive(false);
			difficultySelect.gameObject.SetActive(false);
		}

		public void ShowMainMenu() {
			HideAll();

			settingsMenu.SetActive(false);
			mainMenu.gameObject.SetActive(true);
		}

		public void ShowEditPlayers() {
			HideAll();
			editPlayersDocument.SetVisible(true);
		}

		public void ShowSongSelect() {
			HideAll();
			songSelect.gameObject.SetActive(true);
		}

		public void ShowPreSong() {
			HideAll();
			difficultySelect.gameObject.SetActive(true);
		}

		public void ShowPostSong() {
			HideAll();

			SetupPostSong();
			postSongDocument.SetVisible(true);

			postSong = false;
		}

		public void ToggleSettingsMenu() {
			settingsMenu.SetActive(!settingsMenu.activeSelf);
		}

		public void ShowCalibrationScene() {
			if (PlayerManager.players.Count > 0) {
				GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
			}
		}

		public void ShowHostServerScene() {
			GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
		}

		public void RefreshCache() {
			if (SongLibrary.CacheFile.Exists) {
				File.Delete(SongLibrary.CacheFile.FullName);
				SongLibrary.Reset();
				ShowSongSelect();
			}
		}

		public void Quit() {
			Application.Quit();
		}
	}
}