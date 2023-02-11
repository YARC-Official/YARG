using System;
using System.Collections.Generic;
using System.IO;
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
		private int playerIndex = 0;

		[SerializeField]
		private UIDocument mainMenuDocument;
		[SerializeField]
		private UIDocument editPlayersDocument;
		[SerializeField]
		private UIDocument preSongDocument;
		[SerializeField]
		private UIDocument postSongDocument;

		// Temp
		[SerializeField]
		private Canvas songSelect;

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

			// Bind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent += SignalRecieved;
			}
		}

		private void OnDisable() {
			// Unbind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent -= SignalRecieved;
			}

			// Save player prefs
			PlayerPrefs.Save();
		}

		private void SignalRecieved(string signal) {
			if (signal.StartsWith("DownloadDone,")) {
				Play.song = chosenSong.Duplicate();

				// Replace song folder
				Play.song.realFolderRemote = Play.song.folder;
				Play.song.folder = new(Path.Combine(GameManager.client.remotePath, signal[13..]));

				GameManager.Instance.LoadScene(SceneIndex.PLAY);
			}
		}

		private void Update() {
			UpdateInputWaiting();
			GameManager.client?.CheckForSignals();
		}

		private void SetupMainMenu() {
			var root = mainMenuDocument.rootVisualElement;

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
				GameManager.client.SignalEvent += SignalRecieved;

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

		private void SetupPreSong() {
			// Start the song if all the players chose their instruments
			if (playerIndex >= PlayerManager.players.Count) {
				if (GameManager.client != null) {
					GameManager.client.RequestDownload(chosenSong.folder.FullName);
				} else {
					Play.song = chosenSong;
					GameManager.Instance.LoadScene(SceneIndex.PLAY);
				}
				return;
			}

			var root = preSongDocument.rootVisualElement;

			// Set name label (with bot tag if required)
			var player = PlayerManager.players[playerIndex];
			root.Q<Label>("PlayerNameLabel").text = player.DisplayName;

			// Get option groups
			var instrumentChoice = root.Q<RadioButtonGroup>("InstrumentChoice");
			instrumentChoice.value = 0;
			var difficultyChoice = root.Q<RadioButtonGroup>("DifficultyChoice");
			difficultyChoice.value = 3;

			// Setup button
			var button = root.Q<Button>("Go");
			button.clickable = null; // Remove old events
			button.clicked += () => {
				// Set player stuff
				player.chosenInstrument = instrumentChoice.value switch {
					0 => "guitar",
					1 => "bass",
					2 => "keys",
					3 => "drums",
					4 => "realGuitar",
					5 => "realBass",
					_ => throw new Exception("Unreachable.")
				};
				player.chosenDifficulty = difficultyChoice.value;

				// Refresh for next player
				playerIndex++;
				SetupPreSong();
			};
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
				label.text += $"{player.DisplayName}: {score.percentage * 100f:N1}%, {score.notesHit} hit, {score.notesMissed} missed";

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
			preSongDocument.SetVisible(false);
			postSongDocument.SetVisible(false);
			songSelect.gameObject.SetActive(false);
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

			playerIndex = 0;

			SetupPreSong();
			preSongDocument.SetVisible(true);
		}

		public void ShowPostSong() {
			HideAll();

			SetupPostSong();
			postSongDocument.SetVisible(true);

			postSong = false;
		}
	}
}