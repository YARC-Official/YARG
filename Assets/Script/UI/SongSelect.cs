using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Data;

namespace YARG.UI {
	public class SpngSelect : MonoBehaviour {
		private const int SONG_VIEW_EXTRA = 6;
		private const float INPUT_REPEAT_TIME = 0.05f;
		private const float INPUT_REPEAT_COOLDOWN = 0.5f;

		[SerializeField]
		private GameObject songViewPrefab;
		[SerializeField]
		private GameObject sectionHeaderPrefab;

		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private SelectedSongView selectedSongView;

		private List<SongInfo> songs;

		private List<SongView> songViewsBefore = new();
		private List<SongView> songViewsAfter = new();

		private float inputTimer = 0f;
		private int selectedSongIndex = 0;

		private void Start() {
			SongLibrary.FetchSongs();

			songs = SongLibrary.Songs
				.OrderBy(song => song.SongNameNoParen)
				.ToList();

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

			UpdateSongViews();
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
			selectedSongView.UpdateSongView(songs[selectedSongIndex]);

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
	}
}