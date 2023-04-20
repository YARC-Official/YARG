using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.Settings;

namespace YARG.UI {
	public partial class MainMenu : MonoBehaviour {
		public static bool isPostSong = false;

		public static MainMenu Instance {
			get;
			private set;
		}

		public SongInfo chosenSong = null;

		[SerializeField]
		private Canvas mainMenu;
		[SerializeField]
		private Canvas songSelect;
		[SerializeField]
		private Canvas difficultySelect;
		[SerializeField]
		private Canvas postSong;
		[SerializeField]
		private Canvas editPlayers;
		[SerializeField]
		private Canvas addPlayer;
		[SerializeField]
		private Canvas credits;

		[Space]
		[SerializeField]
		private GameObject menuContainer;
		[SerializeField]
		private GameObject settingsContainer;
		[SerializeField]
		private GameObject songFolderManager;
		[SerializeField]
		private GameObject loadingScreen;
		[SerializeField]
		private TextMeshProUGUI loadingStatus;
		[SerializeField]
		private Image progressBar;

		[SerializeField]
		private TextMeshProUGUI versionText;

		[SerializeField]
		private GameObject updateObject;

		private TextMeshProUGUI updateText;

		private bool isUpdateShown;

		private void Start() {
			Instance = this;

			versionText.text = Constants.VERSION_TAG.ToString();

			updateText = updateObject.GetComponentInChildren<TextMeshProUGUI>();

			if (SongLibrary.SongsByHash == null) {
				RefreshSongLibrary();
			}

			if (!isPostSong) {
				ShowMainMenu();
			} else {
				ShowPostSong();
			}
		}

		private void OnEnable() {
			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
			}
		}

		private void OnDisable() {
			// Save player prefs
			PlayerPrefs.Save();

			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
		}

		private void Update() {
			// Update progress if loading
			if (loadingScreen.activeSelf) {
				progressBar.fillAmount = SongLibrary.loadPercent;
				loadingStatus.text = SongLibrary.currentTaskDescription;

				// Finish loading
				if (!SongLibrary.currentlyLoading) {
					loadingScreen.SetActive(false);
					SongLibrary.loadPercent = 0f;
				}

				return;
			}

			// Update player navigation
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.UpdateNavigationMode();
			}

// 			if (!isUpdateShown && GameManager.Instance.updateChecker.IsOutOfDate) {
// 				isUpdateShown = true;
// 
// 				string newVersion = GameManager.Instance.updateChecker.LatestVersion.ToString();
// 				updateText.text = $"New version available: {newVersion}";
// 				updateObject.gameObject.gameObject.SetActive(true);
// 			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool firstPressed) {
			if (!firstPressed) {
				return;
			}

			if (navigationType == NavigationType.PRIMARY) {
				ShowSongSelect();
			}
		}

		private void HideAll() {
			MainMenuBackground.Instance.cursorMoves = false;

			editPlayers.gameObject.SetActive(false);
			addPlayer.gameObject.SetActive(false);
			mainMenu.gameObject.SetActive(false);
			songSelect.gameObject.SetActive(false);
			difficultySelect.gameObject.SetActive(false);
			postSong.gameObject.SetActive(false);
			credits.gameObject.SetActive(false);
		}

		public void ShowMainMenu() {
			HideAll();

			MainMenuBackground.Instance.cursorMoves = true;

			mainMenu.gameObject.SetActive(true);
			ShowMenuContainer();
		}

		public void ShowEditPlayers() {
			HideAll();
			editPlayers.gameObject.SetActive(true);
		}

		public void ShowAddPlayer() {
			HideAll();
			addPlayer.gameObject.SetActive(true);
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
			postSong.gameObject.SetActive(true);
			isPostSong = false;
		}

		public void ShowCredits() {
			HideAll();
			credits.gameObject.SetActive(true);
		}

		public void HideAllMainMenu() {
			menuContainer.SetActive(false);
			settingsContainer.SetActive(false);
			songFolderManager.SetActive(false);
		}

		public void ShowSettingsMenu() {
			HideAllMainMenu();

			settingsContainer.SetActive(true);
		}

		public void ShowMenuContainer() {
			HideAllMainMenu();

			menuContainer.SetActive(true);
		}

		public void ShowSongFolderManager() {
			HideAllMainMenu();

			songFolderManager.SetActive(true);
		}

		public void ShowCalibrationScene() {
			if (PlayerManager.players.Count > 0) {
				GameManager.Instance.LoadScene(SceneIndex.CALIBRATION);
			}
		}

		public void ShowHostServerScene() {
			GameManager.Instance.LoadScene(SceneIndex.SERVER_HOST);
		}

		public void AbortSongLoad() {
			SettingsManager.DeleteSettingsFile();

			Quit();
		}

		public void RefreshSongLibrary() {
			SongLibrary.Reset();
			ScoreManager.Reset();

			SongLibrary.FetchEverything();
			loadingScreen.SetActive(true);
			ScoreManager.FetchScores();

			SongSelect.refreshFlag = true;
		}

		public void OpenLatestRelease() {
			Application.OpenURL("https://github.com/EliteAsian123/YARG/releases/latest");
		}

		public void Quit() {
#if UNITY_EDITOR

			UnityEditor.EditorApplication.isPlaying = false;

#else

			Application.Quit();

#endif
		}
	}
}