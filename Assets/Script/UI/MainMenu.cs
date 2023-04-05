using System.IO;
using UnityEngine;
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
		private GameObject loadingScreen;
		[SerializeField]
		private Image progressBar;

		private void Start() {
			Instance = this;

			RefreshSongLibrary();

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

				// Finish loading
				if (SongLibrary.loadPercent >= 1f) {
					loadingScreen.SetActive(false);
				}

				return;
			}

			// Update player navigation
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.UpdateNavigationMode();
			}
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

			menuContainer.SetActive(true);
			settingsContainer.SetActive(false);
			mainMenu.gameObject.SetActive(true);
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

		public void ToggleSettingsMenu() {
			menuContainer.SetActive(!menuContainer.activeSelf);
			settingsContainer.SetActive(!settingsContainer.activeSelf);
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
			if (File.Exists(SongLibrary.CacheFile)) {
				File.Delete(SongLibrary.CacheFile);
				RefreshSongLibrary();
			}
		}

		public void AbortSongLoad() {
			SongLibrary.Reset();
			ScoreManager.Reset();

			loadingScreen.SetActive(false);

			SettingsManager.SetSettingValue("songFolder", null);
		}

		public void RefreshSongLibrary() {
			SongLibrary.Reset();
			ScoreManager.Reset();

			bool loading = !SongLibrary.FetchSongs();
			loadingScreen.SetActive(loading);
			ScoreManager.FetchScores();

			SongSelect.refreshFlag = true;
		}

		public void Quit() {
			Application.Quit();
		}
	}
}