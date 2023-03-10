using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;

namespace YARG.UI {
	public class SongSelect : MonoBehaviour {
		public static SongSelect Instance {
			get;
			private set;
		} = null;

		private const int SONG_VIEW_EXTRA = 6;
		private const float INPUT_REPEAT_TIME = 0.035f;
		private const float INPUT_REPEAT_COOLDOWN = 0.5f;

		private class SongOrHeader {
			public SongInfo song;
			public (string, string) header;
		}

		[SerializeField]
		private GameObject songViewPrefab;
		[SerializeField]
		private GameObject sectionHeaderPrefab;

		[Space]
		public TMP_InputField searchField;
		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private SelectedSongView selectedSongView;
		[SerializeField]
		private TMP_Dropdown dropdown;

		[Space]
		[SerializeField]
		private GameObject loadingScreen;
		[SerializeField]
		private Image progressBar;

		private List<SongOrHeader> songs;
		private List<SongInfo> recommendedSongs;

		private List<SongView> songViewsBefore = new();
		private List<SongView> songViewsAfter = new();

		private float inputTimer = 0f;

		// Will be set in UpdateSearch
		private int selectedSongIndex;

		private void Start() {
			Instance = this;

			// Fetch info
			bool loading = !SongLibrary.FetchSongs();
			loadingScreen.SetActive(loading);
			ScoreManager.FetchScores();

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

		private void OnEnable() {
			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
			}

			// Refetch if null (i.e. if a setting changed)
			if (SongLibrary.Songs == null) {
				bool loading = !SongLibrary.FetchSongs();
				loadingScreen.SetActive(loading);
				ScoreManager.FetchScores();

				if (!loading) {
					// Automatically loads songs and updates song views
					recommendedSongs = null;
					UpdateSearch();
				}
			}
		}

		private void OnDisable() {
			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
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

					var songOrHeader = songs[realIndex];
					if (songOrHeader.song != null) {
						songViewsBefore[i].GetComponent<SongView>().UpdateSongView(songOrHeader.song);
					} else {
						songViewsBefore[i].GetComponent<SongView>().UpdateSongViewAsHeader(
							songOrHeader.header.Item1, songOrHeader.header.Item2);
					}
				}
			}

			// Update selected
			if (songs.Count > 0) {
				selectedSongView.gameObject.SetActive(true);
				selectedSongView.UpdateSongView(songs[selectedSongIndex].song);
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

					var songOrHeader = songs[realIndex];
					if (songOrHeader.song != null) {
						songViewsAfter[i].GetComponent<SongView>().UpdateSongView(songOrHeader.song);
					} else {
						songViewsAfter[i].GetComponent<SongView>().UpdateSongViewAsHeader(
							songOrHeader.header.Item1, songOrHeader.header.Item2);
					}
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

					recommendedSongs = null;
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

			// Update player navigation
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.UpdateNavigationMode();
			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool firstPressed) {
			if (inputTimer <= 0f || firstPressed) {
				if (navigationType == NavigationType.UP) {
					inputTimer = firstPressed ? INPUT_REPEAT_COOLDOWN : INPUT_REPEAT_TIME;
					MoveView(-1);
				} else if (navigationType == NavigationType.DOWN) {
					inputTimer = firstPressed ? INPUT_REPEAT_COOLDOWN : INPUT_REPEAT_TIME;
					MoveView(1);
				}
			}

			if (navigationType == NavigationType.PRIMARY) {
				selectedSongView.PlaySong();
			}
		}

		private void MoveView(int amount) {
			selectedSongIndex += amount;

			// Wrap
			if (selectedSongIndex < 0) {
				selectedSongIndex = songs.Count - 1;
			} else if (selectedSongIndex >= songs.Count) {
				selectedSongIndex = 0;
			}

			// Skip over headers
			if (songs[selectedSongIndex].song == null) {
				selectedSongIndex += amount;

				// Wrap again (just in case after skipping)
				if (selectedSongIndex < 0) {
					selectedSongIndex = songs.Count - 1;
				} else if (selectedSongIndex >= songs.Count) {
					selectedSongIndex = 0;
				}
			}

			UpdateSongViews();
		}

		private int FuzzySearch(SongInfo song) {
			if (dropdown.value == 0) {
				return Fuzz.PartialRatio(song.SongName, searchField.text);
			} else {
				return Fuzz.PartialRatio(song.ArtistName, searchField.text);
			}
		}

		public void UpdateSearch() {
			// Get recommended songs
			if (recommendedSongs == null) {
				recommendedSongs = new();

				var mostPlayed = ScoreManager.SongsByPlayCount().Take(10).ToList();
				if (mostPlayed.Count > 0) {
					// Add two random top ten most played songs (ten tries each)
					for (int i = 0; i < 2; i++) {
						for (int t = 0; t < 10; t++) {
							int n = Random.Range(0, mostPlayed.Count);
							if (recommendedSongs.Contains(mostPlayed[n])) {
								continue;
							}

							recommendedSongs.Add(mostPlayed[n]);
							break;
						}
					}

					// Add two random songs from artists that are in the most played (ten tries each)
					for (int i = 0; i < 2; i++) {
						for (int t = 0; t < 10; t++) {
							int n = Random.Range(0, mostPlayed.Count);
							var baseSong = mostPlayed[n];

							// Look all songs by artist
							var sameArtistSongs = SongLibrary.Songs
								.Where(i => i.ArtistName?.ToLower() == baseSong.ArtistName?.ToLower())
								.ToList();
							if (sameArtistSongs.Count <= 1) {
								continue;
							}

							// Pick
							n = Random.Range(0, sameArtistSongs.Count);

							// Skip if included
							if (mostPlayed.Contains(sameArtistSongs[n])) {
								continue;
							}
							if (recommendedSongs.Contains(sameArtistSongs[n])) {
								continue;
							}

							// Add
							recommendedSongs.Add(sameArtistSongs[n]);
							break;
						}
					}
				}

				// Add a completely random song (ten tries)
				for (int t = 0; t < 10; t++) {
					int n = Random.Range(0, SongLibrary.Songs.Count);
					if (recommendedSongs.Contains(SongLibrary.Songs[n])) {
						continue;
					}

					recommendedSongs.Add(SongLibrary.Songs[n]);
					break;
				}

				// Reverse list because we add it backwards
				recommendedSongs.Reverse();
			}

			if (string.IsNullOrEmpty(searchField.text)) {
				// Add all songs
				songs = SongLibrary.Songs
					.OrderBy(song => song.SongNameNoParen)
					.Select(i => new SongOrHeader { song = i })
					.ToList();
				songs.Insert(0, new SongOrHeader { header = ("All Songs", $"{songs.Count} songs") });

				// Add recommended songs
				foreach (var song in recommendedSongs) {
					songs.Insert(0, new SongOrHeader { song = song });
				}
				songs.Insert(0, new SongOrHeader {
					header = ("Recommended Songs", $"{recommendedSongs.Count} random songs")
				});
			} else if (searchField.text.StartsWith("artist:")) {
				// Search by artist
				var artist = searchField.text[7..];
				songs = SongLibrary.Songs
					.Where(i => i.ArtistName.ToLower() == artist.ToLower())
					.OrderBy(song => song.SongNameNoParen)
					.Select(i => new SongOrHeader { song = i })
					.ToList();
				songs.Insert(0, new SongOrHeader { header = ($"{artist}'s Songs", $"{songs.Count} songs") });
			} else {
				// Fuzzy search!
				var text = searchField.text.ToLower();
				songs = SongLibrary.Songs
					.Select(i => new { score = FuzzySearch(i), songInfo = i })
					.Where(i => i.score > 55)
					.OrderByDescending(i => i.score)
					.Select(i => i.songInfo)
					.Select(i => new SongOrHeader { song = i })
					.ToList();
				songs.Insert(0, new SongOrHeader { header = ("Searched Songs", $"{songs.Count} songs") });
			}

			selectedSongIndex = 1;
			UpdateSongViews();
		}
	}
}