using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Data;

namespace YARG.UI {
	public class SongSelect : MonoBehaviour {
		private const int SONG_VIEW_EXTRA = 6;
		private const float INPUT_REPEAT_TIME = 0.05f;
		private const float INPUT_REPEAT_COOLDOWN = 0.5f;

		[SerializeField]
		private GameObject songViewPrefab;
		[SerializeField]
		private GameObject sectionHeaderPrefab;

		[Space]
		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private SelectedSongView selectedSongView;
		[SerializeField]
		private TMP_InputField searchField;

		[Space]
		[SerializeField]
		private GameObject loadingScreen;
		[SerializeField]
		private Image progressBar;

		private List<SongInfo> songs;

		private List<SongView> songViewsBefore = new();
		private List<SongView> songViewsAfter = new();

		private float inputTimer = 0f;
		private int selectedSongIndex = 0;

		private void Start() {
			bool loading = !SongLibrary.FetchSongs();
			loadingScreen.SetActive(loading);

			// Create before (insert backwards)
			for (int i = 0; i < SONG_VIEW_EXTRA; i++) {
				var gameObject = Instantiate(songViewPrefab, songListContent);
				gameObject.transform.SetAsFirstSibling();

				songViewsBefore.Add(gameObject.GetComponent<SongView>());
			}

			// Create after
			for (int i = 0; i < SONG_VIEW_EXTRA; i++) {
				var gameObject = Instantiate(songViewPrefab, songListContent);
				gameObject.transform.SetAsLastSibling(); // Good measure

				songViewsAfter.Add(gameObject.GetComponent<SongView>());
			}

			if (!loading) {
				// Automatically loads songs and updates song views
				UpdateSearch();
			}
		}

		private void UpdateSongViews() {
			// Update before
			for (int i = 0; i < SONG_VIEW_EXTRA; i++) {
				// Song views are inserted backwards, so this works.
				int realIndex = selectedSongIndex - i - 1;

				if (realIndex < 0) {
					songViewsBefore[i].GetComponent<CanvasGroup>().alpha = 0f;
				} else {
					songViewsBefore[i].GetComponent<CanvasGroup>().alpha = 1f;
					songViewsBefore[i].GetComponent<SongView>().UpdateSongView(songs[realIndex]);
				}
			}

			// Update selected
			if (songs.Count > 0) {
				selectedSongView.gameObject.SetActive(true);
				selectedSongView.UpdateSongView(songs[selectedSongIndex]);
			} else {
				selectedSongView.gameObject.SetActive(false);
			}

			// Update after
			for (int i = 0; i < SONG_VIEW_EXTRA; i++) {
				int realIndex = selectedSongIndex + i + 1;

				if (realIndex >= songs.Count) {
					songViewsAfter[i].GetComponent<CanvasGroup>().alpha = 0f;
				} else {
					songViewsAfter[i].GetComponent<CanvasGroup>().alpha = 1f;
					songViewsAfter[i].GetComponent<SongView>().UpdateSongView(songs[realIndex]);
				}
			}
		}

		private void Update() {
			// Update progress if loading

			if (loadingScreen.activeSelf) {
				progressBar.fillAmount = SongLibrary.loadPercent;

				// Finish loading
				if (SongLibrary.loadPercent >= 1f) {
					loadingScreen.SetActive(false);
					UpdateSearch();
				}

				return;
			}

			// Update input timer

			inputTimer -= Time.deltaTime;

			// Up arrow

			if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
				inputTimer = INPUT_REPEAT_COOLDOWN;
				MoveView(-1);
			}

			if (Keyboard.current.upArrowKey.isPressed && inputTimer <= 0f) {
				inputTimer = INPUT_REPEAT_TIME;
				MoveView(-1);
			}

			// Down arrow

			if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
				inputTimer = INPUT_REPEAT_COOLDOWN;
				MoveView(1);
			}

			if (Keyboard.current.downArrowKey.isPressed && inputTimer <= 0f) {
				inputTimer = INPUT_REPEAT_TIME;
				MoveView(1);
			}

			// Scroll wheel

			var scroll = Mouse.current.scroll.ReadValue().y;
			if (scroll > 0f) {
				MoveView(-1);
			} else if (scroll < 0f) {
				MoveView(1);
			}
		}

		private void MoveView(int amount) {
			selectedSongIndex += amount;
			if (selectedSongIndex >= songs.Count) {
				selectedSongIndex = 0;
			} else if (selectedSongIndex < 0) {
				selectedSongIndex = songs.Count - 1;
			}

			UpdateSongViews();
		}

		public void UpdateSearch() {
			IEnumerable<SongInfo> songInfos;
			if (string.IsNullOrEmpty(searchField.text)) {
				songInfos = SongLibrary.Songs
					.Where(song => true);
			} else {
				// TODO: fuzzy search
				var text = searchField.text.ToLower();
				songInfos = SongLibrary.Songs
					.Where(song => song.SongName.ToLower().StartsWith(text) || song.ArtistName.ToLower().StartsWith(text));
			}

			songs = songInfos
				.OrderBy(song => song.SongNameNoParen)
				.ToList();

			selectedSongIndex = 0;
			UpdateSongViews();
		}
	}
}