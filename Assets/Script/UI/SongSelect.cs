using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Data;
using YARG.Input;

namespace YARG.UI {
	public class SongSelect : MonoBehaviour {
		public static SongSelect Instance {
			get;
			private set;
		} = null;

		public static bool refreshFlag = true;

		private const int SONG_VIEW_EXTRA = 6;
		private const float INPUT_REPEAT_TIME = 0.035f;
		private const float INPUT_REPEAT_COOLDOWN = 0.5f;

		private class SongOrHeader {
			public SongInfo song;
			public (string, string) header;
		}

		[SerializeField]
		private GameObject songViewPrefab;

		[Space]
		public TMP_InputField searchField;
		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private SelectedSongView selectedSongView;
		[SerializeField]
		private TMP_Dropdown dropdown;
		[SerializeField]
		private GameObject noSongsText;

		private List<SongOrHeader> songs;
		private List<SongInfo> recommendedSongs;

		private List<SongView> songViewsBefore = new();
		private List<SongView> songViewsAfter = new();

		private float inputTimer = 0f;

		// Will be set in UpdateSearch
		private int selectedSongIndex;

		private void Awake() {
			refreshFlag = true;
			Instance = this;

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
		}

		private void OnEnable() {
			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
			}

			if (refreshFlag) {
				songs = null;
				recommendedSongs = null;

				// Get songs
				UpdateSearch();
				refreshFlag = false;
			}
		}

		private void OnDisable() {
			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
		}

		private void UpdateSongViews() {
			noSongsText.SetActive(songs.Count <= 0);
			selectedSongView.gameObject.SetActive(songs.Count >= 1);

			// Skip rest if no songs
			if (songs.Count <= 0) {
				// Hide all song views
				for (int i = 0; i < SONG_VIEW_EXTRA; i++) {
					songViewsBefore[i].GetComponent<CanvasGroup>().alpha = 0f;
					songViewsAfter[i].GetComponent<CanvasGroup>().alpha = 0f;
				}

				return;
			}

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

			if (!firstPressed) {
				return;
			}

			if (navigationType == NavigationType.PRIMARY) {
				selectedSongView.PlaySong();
			} else if (navigationType == NavigationType.SECONDARY) {
				Back();
			} else if (navigationType == NavigationType.TERTIARY) {
				if (songs.Count > 0) {
					searchField.text = $"artist:{songs[selectedSongIndex].song.artistName}";
				}
			}
		}

		private void MoveView(int amount) {
			if (songs.Count <= 0) {
				return;
			}

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

		private int FuzzySearch(string text, SongInfo song) {
			if (dropdown.value == 0) {
				return Fuzz.PartialRatio(song.SongName, text);
			} else {
				return Fuzz.PartialRatio(song.artistName, text);
			}
		}

		public void UpdateSearch() {
			// Get recommended songs
			if (recommendedSongs == null) {
				recommendedSongs = new();

				if (SongLibrary.Songs.Count > 0) {
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
									.Where(i => i.artistName?.ToLower() == baseSong.artistName?.ToLower())
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
					var songsAsArray = SongLibrary.Songs.ToArray();
					for (int t = 0; t < 10; t++) {
						int n = Random.Range(0, songsAsArray.Length);
						if (recommendedSongs.Contains(songsAsArray[n])) {
							continue;
						}

						recommendedSongs.Add(songsAsArray[n]);
						break;
					}

					// Reverse list because we add it backwards
					recommendedSongs.Reverse();
				}
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
					header = ("Recommended Songs", $"{recommendedSongs.Count} songs")
				});
			} else {
				// Split up args
				var split = searchField.text.Split(';');
				IEnumerable<SongInfo> songsOut = SongLibrary.Songs;

				// Go through them all
				bool fuzzySearched = false;
				foreach (var arg in split) {
					if (arg.StartsWith("artist:")) {
						// Artist filter
						var artist = arg[7..];
						songsOut = SongLibrary.Songs
							.Where(i => i.artistName.ToLower() == artist.ToLower());
					} else if (arg.StartsWith("source:")) {
						// Source filter
						var source = arg[7..];
						songsOut = SongLibrary.Songs
							.Where(i => i.source.ToLower() == source.ToLower());
					} else if (arg.StartsWith("album:")) {
						// Album filter
						var album = arg[6..];
						songsOut = SongLibrary.Songs
							.Where(i => i.album.ToLower() == album.ToLower());
					} else if (arg.StartsWith("genre:")) {
						// Genre filter
						var genre = arg[6..];
						songsOut = SongLibrary.Songs
							.Where(i => i.genre.ToLower() == genre.ToLower());
					} else if (arg.StartsWith("year:")) {
						// Year filter
						var year = arg[5..];
						songsOut = SongLibrary.Songs
							.Where(i => i.year.ToLower() == year.ToLower());
					} else if (arg.StartsWith("charter:")) {
						// Charter filter
						var charter = arg[8..];
						songsOut = SongLibrary.Songs
							.Where(i => (i.charter ?? "Unknown").ToLower() == charter?.ToLower())
							.ToList();
					} else if (!fuzzySearched) {
						// Fuzzy search
						fuzzySearched = true;
						songsOut = songsOut
							.Select(i => new { score = FuzzySearch(arg, i), songInfo = i })
							.Where(i => i.score > 55)
							.OrderByDescending(i => i.score)
							.Select(i => i.songInfo);
					}
				}

				// Sort
				if (!fuzzySearched) {
					songsOut = songsOut.OrderBy(song => song.SongNameNoParen);
				}

				// Add header
				songs = songsOut.Select(i => new SongOrHeader { song = i }).ToList();
				songs.Insert(0, new SongOrHeader { header = ($"Search Results", $"{songs.Count} " + (songs.Count == 1 ? "song" : "songs") )});
			}

			// Count songs
			int songCount = 0;
			foreach (var songOrHeader in songs) {
				if (songOrHeader.song != null) {
					songCount++;
				}
			}

			// If there are no songs, remove the headers
			if (songCount <= 0) {
				songs.Clear();
			}

			selectedSongIndex = 1;
			UpdateSongViews();
		}

		public void Back() {
			if (string.IsNullOrEmpty(searchField.text)) {
				MainMenu.Instance.ShowMainMenu();
			} else {
				searchField.text = "";
				UpdateSearch();
			}
		}
	}
}