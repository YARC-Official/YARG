using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YARG.Data;
using YARG.Play;
using YARG.Serialization;
using YARG.Utils;

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
		}

		private void SignalRecieved(string signal) {
			if (signal.StartsWith("DownloadDone,")) {
				PlayManager.song = SongIni.CompleteSongInfo(new SongInfo(
					new(Path.Combine(GameManager.client.remotePath, signal[13..^0]))
				));
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
					PlayManager.song = chosenSong;
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
					1 => "bass",
					2 => "keys",
					0 or _ => "guitar"
				};
				player.chosenDifficulty = difficultyChoice.value;

				// Refresh for next player
				playerIndex++;
				SetupPreSong();
			};
		}

		private void SetupPostSong() {
			var root = postSongDocument.rootVisualElement;

			// Setup score label

			var label = root.Q<Label>("Score");
			label.text = "";

			foreach (var player in PlayerManager.players) {
				if (!player.lastScore.HasValue) {
					continue;
				}

				var score = player.lastScore.Value;
				label.text += $"{player.DisplayName}: {score.percentage * 100f:N1}%, {score.notesHit} hit, {score.notesMissed} missed\n\n";
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