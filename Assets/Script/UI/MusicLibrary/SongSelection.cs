using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Input;
using YARG.Song;
using YARG.UI.MusicLibrary.ViewTypes;
using Random = UnityEngine.Random;

namespace YARG.UI.MusicLibrary {
	public class SongSelection : MonoBehaviour {
		public static SongSelection Instance {
			get;
			private set;
		} = null;

		public static bool refreshFlag = true;

		private const int SONG_VIEW_EXTRA = 6;

		[SerializeField]
		private GameObject songViewPrefab;

		[Space]
		public TMP_InputField searchField;
		[SerializeField]
		private Transform songListContent;
		[SerializeField]
		private Sidebar sidebar;
		[SerializeField]
		private GameObject noSongsText;
		[SerializeField]
		private Scrollbar scrollbar;

		private List<ViewType> _songs;
		private List<SongEntry> _recommendedSongs;

		public IReadOnlyList<ViewType> Songs => _songs;

		private int _selectedIndex;
		public int SelectedIndex {
			get => _selectedIndex;
			private set {
				_selectedIndex = value;

				if (_songs.Count <= 0) {
					return;
				}

				// Wrap
				if (_selectedIndex < 0) {
					_selectedIndex = _songs.Count - 1;
				} else if (_selectedIndex >= _songs.Count) {
					_selectedIndex = 0;
				}

				UpdateScrollbar();
				UpdateSongViews();

				if (_songs[_selectedIndex] is not SongViewType song) {
					return;
				}

				if (song.SongEntry == GameManager.Instance.SelectedSong) {
					return;
				}

				GameManager.Instance.SelectedSong = song.SongEntry;
				if (!refreshFlag) {
					GameManager.AudioManager.StartPreviewAudio().Forget();
				}
			}
		}

		private List<SongView> songViews = new();

		private float scroll;
		private bool isSelectingStopped = true;

		private void Awake() {
			refreshFlag = true;
			Instance = this;

			// Create all of the song views
			for (int i = 0; i < SONG_VIEW_EXTRA * 2 + 1; i++) {
				var gameObject = Instantiate(songViewPrefab, songListContent);

				// Init and add
				var songView = gameObject.GetComponent<SongView>();
				songView.Init(i - SONG_VIEW_EXTRA);
				songViews.Add(songView);
			}

			// Initialize sidebar
			sidebar.Init();
		}

		private void OnEnable() {
			// Set navigation scheme
			Navigator.Instance.PushScheme(new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Up, "Up", () => {
					SelectedIndex--;
				}),
				new NavigationScheme.Entry(MenuAction.Down, "Down", () => {
					SelectedIndex++;
				}),
				new NavigationScheme.Entry(MenuAction.Confirm, "Confirm", () => {
					_songs[SelectedIndex]?.PrimaryButtonClick();
				}),
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => {
					Back();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut1, "Search Artist", () => {
					if (_songs[SelectedIndex] is SongViewType view) {
						searchField.text = $"artist:{view.SongEntry.Artist}";
					}
				})
			}));

			if (refreshFlag) {
				_songs = null;
				_recommendedSongs = null;

				// Get songs
				UpdateSearch();
				refreshFlag = false;
			}
			GameManager.AudioManager.StartPreviewAudio().Forget();
		}

		private void OnDisable() {
			Navigator.Instance.PopScheme();
		}

		private void UpdateSongViews() {
			foreach (var songView in songViews) {
				songView.UpdateView();
			}

			sidebar.UpdateSidebar().Forget();
		}

		private void Update() {
			// Scroll wheel

			var scroll = Mouse.current.scroll.ReadValue().y;
			if (scroll > 0f) {
				SelectedIndex--;
				isSelectingStopped = false;
			} else if (scroll < 0f) {
				SelectedIndex++;
				isSelectingStopped = false;
			} else if (Mathf.Abs(scroll) < float.Epsilon && !isSelectingStopped) {
				if (_songs[SelectedIndex] is SongViewType) {
					// GameManager.AudioManager.StartPreviewAudio();
					isSelectingStopped = true;
				}
			}
		}

		public void OnScrollBarChange() {
			SelectedIndex = Mathf.FloorToInt(scrollbar.value * (_songs.Count - 1));
		}

		private void UpdateScrollbar() {
			scrollbar.SetValueWithoutNotify((float) SelectedIndex / _songs.Count);
		}

		public void UpdateSearch() {
			// Get recommended songs
			if (_recommendedSongs == null) {
				_recommendedSongs = new();

				if (SongContainer.Songs.Count > 0) {
					FillRecommendedSongs();
				}
			}

			if (string.IsNullOrEmpty(searchField.text)) {
				// Add all songs
				_songs = SongContainer.Songs
					.OrderBy(song => song.NameNoParenthesis)
					.Select(i => new SongViewType(i))
					.Cast<ViewType>()
					.ToList();
				_songs.Insert(0, new CategoryViewType(
					"ALL SONGS",
					$"<#00B6F5><b>{_songs.Count}</b> <#006488>{(_songs.Count == 1 ? "SONG" : "SONGS")}",
					SongContainer.Songs
				));

				// Add recommended songs
				foreach (var song in _recommendedSongs) {
					_songs.Insert(0, new SongViewType(song));
				}
				_songs.Insert(0, new CategoryViewType(
					_recommendedSongs.Count == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
					$"<#00B6F5><b>{_recommendedSongs.Count}</b> <#006488>{(_recommendedSongs.Count == 1 ? "SONG" : "SONGS")}",
					_recommendedSongs
				));

				// Add buttons
				_songs.Insert(0, new ButtonViewType(
					"RANDOM SONG",
					"Icon/Random",
					() => {
						// Get how many non-song things there are
						int skip = _songs.Count - SongContainer.Songs.Count;

						// Select random between all of the songs
						SelectedIndex = Random.Range(skip, SongContainer.Songs.Count);
					}
				));
			} else {
				// Split up args
				var split = searchField.text.Split(';');
				IEnumerable<SongEntry> songsOut = SongContainer.Songs;

				// Go through them all
				bool searched = false;
				foreach (var arg in split) {
					if (arg.StartsWith("artist:")) {
						// Artist filter
						var artist = arg[7..];
						songsOut = SongContainer.Songs
							.Where(i => i.Artist?.ToLower() == artist.ToLower());
					} else if (arg.StartsWith("source:")) {
						// Source filter
						var source = arg[7..];
						songsOut = SongContainer.Songs
							.Where(i => i.Source?.ToLower() == source.ToLower());
					} else if (arg.StartsWith("album:")) {
						// Album filter
						var album = arg[6..];
						songsOut = SongContainer.Songs
							.Where(i => i.Album?.ToLower() == album.ToLower());
					} else if (arg.StartsWith("charter:")) {
						// Charter filter
						var charter = arg[8..];
						songsOut = SongContainer.Songs
							.Where(i => i.Charter?.ToLower() == charter.ToLower());
					} else if (arg.StartsWith("year:")) {
						// Year filter
						var year = arg[5..];
						songsOut = SongContainer.Songs
							.Where(i => i.Year?.ToLower() == year.ToLower());
					} else if (arg.StartsWith("genre:")) {
						// Genre filter
						var genre = arg[6..];
						songsOut = SongContainer.Songs
							.Where(i => i.Genre?.ToLower() == genre.ToLower());
					} else if (!searched) {
						// Search
						searched = true;
						songsOut = songsOut
							.Select(i => new { score = Search(arg, i), songInfo = i })
							.Where(i => i.score >= 0)
							.OrderBy(i => i.score)
							.Select(i => i.songInfo);
					}
				}

				// Sort
				if (!searched) {
					songsOut = songsOut.OrderBy(song => song.NameNoParenthesis);
				}

				// Add header
				_songs = songsOut.Select(i => new SongViewType(i)).Cast<ViewType>().ToList();
				_songs.Insert(0, new CategoryViewType(
					"SEARCH RESULTS",
					$"<#00B6F5><b>{_songs.Count}</b> <#006488>{(_songs.Count == 1 ? "SONG" : "SONGS")}"
				));
			}

			// Count songs
			int songCount = 0;
			foreach (var viewType in _songs) {
				if (viewType is SongViewType) {
					songCount++;
				}
			}

			// If there are no songs, remove the headers
			if (songCount <= 0) {
				_songs.Clear();
			}

			if (GameManager.Instance.SelectedSong == null) {
				if (string.IsNullOrEmpty(searchField.text)) {
					SelectedIndex = 2;
				} else {
					SelectedIndex = 1;
				}
			} else {
				var index = _songs.FindIndex(song => {
					return song is SongViewType songType && songType.SongEntry == GameManager.Instance.SelectedSong;
				});

				SelectedIndex = Mathf.Max(1, index);
			}

			UpdateSongViews();
			UpdateScrollbar();
		}

		private int Search(string input, SongEntry songInfo) {
			string i = input.ToLowerInvariant();

			// Get scores
			int nameIndex = songInfo.NameNoParenthesis.ToLowerInvariant().IndexOf(i);
			int artistIndex = songInfo.Artist.ToLowerInvariant().IndexOf(i, StringComparison.Ordinal);

			// Return the best search
			if (nameIndex == -1 && artistIndex == -1) {
				return -1;
			} else if (nameIndex == -1) {
				return artistIndex;
			} else if (artistIndex == -1) {
				return nameIndex;
			} else {
				return Mathf.Min(nameIndex, artistIndex);
			}
		}

		private void FillRecommendedSongs() {
			var mostPlayed = ScoreManager.SongsByPlayCount().Take(10).ToList();
			if (mostPlayed.Count > 0) {
				// Add two random top ten most played songs (ten tries each)
				for (int i = 0; i < 2; i++) {
					for (int t = 0; t < 10; t++) {
						int n = Random.Range(0, mostPlayed.Count);
						if (_recommendedSongs.Contains(mostPlayed[n])) {
							continue;
						}

						_recommendedSongs.Add(mostPlayed[n]);
						break;
					}
				}

				// Add two random songs from artists that are in the most played (ten tries each)
				for (int i = 0; i < 2; i++) {
					for (int t = 0; t < 10; t++) {
						int n = Random.Range(0, mostPlayed.Count);
						var baseSong = mostPlayed[n];

						// Look all songs by artist
						var sameArtistSongs = SongContainer.Songs
							.Where(i => i.Artist?.ToLower() == baseSong.Artist?.ToLower())
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
						if (_recommendedSongs.Contains(sameArtistSongs[n])) {
							continue;
						}

						// Add
						_recommendedSongs.Add(sameArtistSongs[n]);
						break;
					}
				}
			}

			// Add a completely random song (ten tries)
			var songsAsArray = SongContainer.Songs;
			for (int t = 0; t < 10; t++) {
				int n = Random.Range(0, songsAsArray.Count);
				if (_recommendedSongs.Contains(songsAsArray[n])) {
					continue;
				}

				_recommendedSongs.Add(songsAsArray[n]);
				break;
			}

			// Reverse list because we add it backwards
			_recommendedSongs.Reverse();
		}

		public void Back() {
			if (string.IsNullOrEmpty(searchField.text)) {
				GameManager.AudioManager.StopPreviewAudio();
				MainMenu.Instance.ShowMainMenu();
			} else {
				searchField.text = "";
				UpdateSearch();
			}
		}
	}
}