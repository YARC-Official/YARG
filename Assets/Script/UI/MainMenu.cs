using System;
using System.Collections.Generic;
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
		private UIDocument mainMenuDocument;
		[SerializeField]
		private UIDocument editPlayersDocument;
		[SerializeField]
		private UIDocument postSongDocument;

		[SerializeField]
		private Canvas songSelect;
		[SerializeField]
		private Canvas difficultySelect;

		private void Start() {
			Instance = this;

			// Load song folder from player prefs
			if (!string.IsNullOrEmpty(PlayerPrefs.GetString("songFolder"))) {
				SongLibrary.songFolder = new(PlayerPrefs.GetString("songFolder"));
			}

			SetupMainMenu();
			SetupEditPlayers();

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

		private void Update() {
			UpdateInputWaiting();
			GameManager.client?.CheckForSignals();
		}

		private void SetupMainMenu() {
			var root = mainMenuDocument.rootVisualElement;

			root.Q<Button>("Browse").clicked += ShowSongFolderSelect;
			root.Q<Button>("PlayButton").clicked += ShowSongSelect;
			root.Q<Button>("EditPlayersButton").clicked += ShowEditPlayers;
			root.Q<Button>("HostServer").clicked += () => GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
			root.Q<Button>("CalibrationButton").clicked += () => {
				if (PlayerManager.players.Count > 0) {
					GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
				}
			};

			// Folder
			var folder = root.Q<TextField>("Folder");
			folder.value = SongLibrary.songFolder.FullName;
			folder.RegisterValueChangedCallback(e => {
				if (folder != e.target) {
					return;
				}

				SongLibrary.songFolder = new(folder.value);
				PlayerPrefs.SetString("songFolder", folder.value);
			});

			// Join server
			root.Q<Button>("JoinServer").clicked += () => {
				var ip = root.Q<TextField>("ServerIP").value;

				// Start + bind
				GameManager.client = new();
				GameManager.client.Start(ip);

				// Hide button
				root.Q<Button>("JoinServer").SetOpacity(0f);
			};

			// Low quality toggle
			var lowQuality = root.Q<Toggle>("LowQuality");
			lowQuality.value = GameManager.Instance.LowQualityMode;
			lowQuality.RegisterValueChangedCallback(e => {
				if (lowQuality != e.target) {
					return;
				}

				GameManager.Instance.LowQualityMode = lowQuality.value;
			});

			// Calibration
			var calibrationField = root.Q<FloatField>("Calibration");
			calibrationField.value = PlayerManager.globalCalibration;
			calibrationField.RegisterValueChangedCallback(e => {
				if (calibrationField != e.target) {
					return;
				}

				PlayerManager.globalCalibration = calibrationField.value;
			});
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
			mainMenuDocument.SetVisible(false);
			editPlayersDocument.SetVisible(false);
			postSongDocument.SetVisible(false);

			songSelect.gameObject.SetActive(false);
			difficultySelect.gameObject.SetActive(false);
		}

		public void ShowMainMenu() {
			HideAll();
			mainMenuDocument.SetVisible(true);
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

		public void ShowSongFolderSelect() {
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", null, false, folder => {
				mainMenuDocument.rootVisualElement.Q<TextField>("Folder").value = folder[0];
			});
		}
	}
}