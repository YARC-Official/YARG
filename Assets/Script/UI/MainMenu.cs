using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YARG.Server;
using YARG.Utils;

namespace YARG.UI {
	public partial class MainMenu : MonoBehaviour {
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

		// Temp
		[SerializeField]
		private Canvas songSelect;

		private void Start() {
			Instance = this;

			// Stop client on quit
			Application.quitting += Client.Stop;

			SetupMainMenu();
			SetupEditPlayers();

			ShowMainMenu();
		}

		private void Update() {
			UpdateInputWaiting();
		}

		private void SetupMainMenu() {
			var root = mainMenuDocument.rootVisualElement;

			root.Q<Button>("PlayButton").clicked += ShowSongSelect;
			root.Q<Button>("EditPlayersButton").clicked += ShowEditPlayers;
			root.Q<Button>("HostServer").clicked += () => {
				SceneManager.LoadScene(2);
			};

			root.Q<Button>("JoinServer").clicked += () => {
				var ip = root.Q<TextField>("ServerIP").value;
				Client.Start(ip);

				// Hide button
				root.Q<Button>("JoinServer").SetOpacity(0f);
			};

			// Low quality toggle
			var lowQuality = root.Q<Toggle>("LowQuality");
			lowQuality.value = PlayerManager.LowQualityMode;
			lowQuality.RegisterValueChangedCallback(e => {
				if (lowQuality != e.target) {
					return;
				}

				PlayerManager.LowQualityMode = lowQuality.value;
			});
		}

		private void SetupPreSong() {
			// Start the song if all the players chose their instruments
			if (playerIndex >= PlayerManager.players.Count) {
				if (Menu.remoteMode) {
					Menu.DownloadSong(chosenSong);
				} else {
					Game.song = chosenSong.folder;
					SceneManager.LoadScene(1);
				}
				return;
			}

			var root = preSongDocument.rootVisualElement;

			// Set name label (with bot tag if required)
			var player = PlayerManager.players[playerIndex];
			root.Q<Label>("PlayerNameLabel").text = player.name + (player.inputStrategy.botMode ? " (BOT)" : "");

			// Get option groups
			var instrumentChoice = root.Q<RadioButtonGroup>("InstrumentChoice");
			instrumentChoice.value = -1;
			var difficultyChoice = root.Q<RadioButtonGroup>("DifficultyChoice");
			difficultyChoice.value = -1;

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

		private void HideAll() {
			mainMenuDocument.SetVisible(false);
			editPlayersDocument.SetVisible(false);
			preSongDocument.SetVisible(false);
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
	}
}