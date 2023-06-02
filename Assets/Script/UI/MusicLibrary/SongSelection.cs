using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Data;
using YARG.Input;
using YARG.Song;
using YARG.UI.MusicLibrary.ViewTypes;
using Random = UnityEngine.Random;

namespace YARG.UI.MusicLibrary {
	public class SongSelection : MonoBehaviour {
		private enum SortCriteria {
			SongName = 1,
			ArtistName,
			SourceName,
		}

		public static SongSelection Instance { get; private set; }

		public static bool refreshFlag = true;

		private const int SONG_VIEW_EXTRA = 15;
		private const float SCROLL_TIME = 1f / 60f;

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

		private SortCriteria _sortCriteria = SortCriteria.SongName;

		private List<ViewType> _songs;
		private List<SongEntry> _recommendedSongs;

		private PreviewContext _previewContext;

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
				_previewContext.PlayPreview(song.SongEntry).Forget();
			}
		}

		private List<SongView> _songViews = new();
		private float _scrollTimer = 0f;
		private bool searchBoxShouldBeEnabled = false;

		private List<char> songsFirstLetter;

		private void Awake() {
			refreshFlag = true;
			Instance = this;

			// Create all of the song views
			for (int i = 0; i < SONG_VIEW_EXTRA * 2 + 1; i++) {
				var gameObject = Instantiate(songViewPrefab, songListContent);

				// Init and add
				var songView = gameObject.GetComponent<SongView>();
				songView.Init(i - SONG_VIEW_EXTRA);
				_songViews.Add(songView);
			}

			// Initialize sidebar
			sidebar.Init();
		}

		private void OnEnable() {
			// Set up preview context
			_previewContext = new(GameManager.AudioManager);

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
				new NavigationScheme.Entry(MenuAction.Shortcut1, "Change Order", () => {
					ChangeSongOrder();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut2, "Search Artist/Source", () => {
					ChangeFilter();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut3, "Next section", () => {
					SelectNextSection();
				})
			}, false));

			if (refreshFlag) {
				_songs = null;
				_recommendedSongs = null;

				// Get songs
				UpdateSearch();
				refreshFlag = false;
			}

			searchBoxShouldBeEnabled = true;

			// Play preview on enter for selected song
			if (_songs[SelectedIndex] is SongViewType song) {
				_previewContext.PlayPreview(song.SongEntry).Forget();
			}
		}

		private void OnDisable() {
			Navigator.Instance.PopScheme();

			_previewContext.Dispose();
			_previewContext = null;
		}

		private void UpdateSongViews() {
			foreach (var songView in _songViews) {
				songView.UpdateView();
			}

			sidebar.UpdateSidebar().Forget();
		}

		private void ChangeSongOrder() {
			if (_sortCriteria == SortCriteria.SongName) {
				_sortCriteria = SortCriteria.ArtistName;
			} else if (_sortCriteria == SortCriteria.ArtistName) {
				_sortCriteria = SortCriteria.SourceName;
			} else {
				_sortCriteria = SortCriteria.SongName;
			}
			UpdateSearch();
		}

		private string GetSortName(SongEntry song) {
			if (_sortCriteria == SortCriteria.ArtistName) {
				return song.Artist;
			} else if (_sortCriteria == SortCriteria.SourceName) {
				return song.Source;
			} else {
				return song.NameNoParenthesis;
			}
		}

		private void ChangeFilter() {
			if (_songs[SelectedIndex] is SongViewType view) {
				if (string.IsNullOrEmpty(searchField.text) || searchField.text.StartsWith("source:")) {
					searchField.text = $"artist:{view.SongEntry.Artist}";
				} else if (searchField.text.StartsWith("artist:")) {
					searchField.text = $"source:{view.SongEntry.Source}";
				}
			}
		}

		private void Update() {
			if (_scrollTimer <= 0f) {
				var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

				if (delta > 0f) {
					SelectedIndex--;
					_scrollTimer = SCROLL_TIME;
				} else if (delta < 0f) {
					SelectedIndex++;
					_scrollTimer = SCROLL_TIME;
				}
			} else {
				_scrollTimer -= Time.deltaTime;
			}

			if (Keyboard.current.escapeKey.wasPressedThisFrame) {
				ClearSearchBox();
			}

			if (searchBoxShouldBeEnabled) {
				searchField.ActivateInputField();
				searchBoxShouldBeEnabled = false;
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
					.OrderBy(song => GetSortName(song))
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
						SelectRandomSong();
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
							.Where(i => RemoveDiacritics(i.Artist) == RemoveDiacritics(artist));
					} else if (arg.StartsWith("source:")) {
						// Source filter
						var source = arg[7..];
						songsOut = SongContainer.Songs
							.Where(i => i.Source?.ToLower() == source.ToLower());
					} else if (arg.StartsWith("album:")) {
						// Album filter
						var album = arg[6..];
						songsOut = SongContainer.Songs
							.Where(i => RemoveDiacritics(i.Album) == RemoveDiacritics(album));
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
					} else if (arg.StartsWith("instrument:")) {
						// Instrument filter
						var instrument = arg[11..];
						/*f (instrument == "band") {
							songsOut = SongContainer.Songs
								.Where(i => i.BandDifficulty >= 0);
						} else {
							songsOut = SongContainer.Songs
								.Where(i => i.HasInstrument(InstrumentHelper.FromStringName(instrument)));
						}*/
						songsOut = instrument switch {
							"band" => SongContainer.Songs.Where(i => i.BandDifficulty >= 0),
							"vocals" => SongContainer.Songs.Where(i => i.VocalParts < 2),
							"harmVocals" => SongContainer.Songs.Where(i => i.VocalParts >= 2),
							_ => SongContainer.Songs.Where(i =>
								i.HasInstrument(InstrumentHelper.FromStringName(instrument))),
						};
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
					songsOut = songsOut.OrderBy(song => GetSortName(song));
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

			SetFirstLetters();
			UpdateSongViews();
			UpdateScrollbar();
		}

		private void SetFirstLetters(){
			songsFirstLetter =
				_songs
				.OfType<SongViewType>()
				.Select(song => GetSortName(song.SongEntry))
				.Where(name => !string.IsNullOrEmpty(name))
				.Select(name => Char.ToUpper(name[0]))
				.Distinct()
				.OrderBy(ch => ch)
				.ToList();
		}

		private static string RemoveDiacritics(string text) {
			if (text == null) {
				return null;
			}

			var normalizedString = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
			var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

			for (int i = 0; i < normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) {
					stringBuilder.Append(c);
				}
			}

			return stringBuilder
				.ToString()
				.Normalize(NormalizationForm.FormC);
		}

		private int Search(string input, SongEntry songInfo) {
			string normalizedInput = RemoveDiacritics(input);

			// Get name index
			string name = songInfo.NameNoParenthesis;
			int nameIndex = RemoveDiacritics(name).IndexOf(normalizedInput);

			// Get artist index
			string artist = songInfo.Artist;
			int artistIndex = RemoveDiacritics(artist).IndexOf(normalizedInput, StringComparison.Ordinal);

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
				if (SelectedIndex > 2) {
					SelectedIndex = 2;
				} else {
					MainMenu.Instance.ShowMainMenu();
				}
			} else {
				ClearSearchBox();
				UpdateSearch();
			}
		}

		private void ClearSearchBox() {
			searchField.text = "";
			searchField.ActivateInputField();
		}

		private void SelectRandomSong(){
			// Get how many non-song things there are
			int skip = _songs.Count - SongContainer.Songs.Count;
			// Select random between all of the songs
			SelectedIndex = Random.Range(skip, SongContainer.Songs.Count);
		}

		private void SelectNextSection(){
			if (_songs[_selectedIndex] is not SongViewType song) {
				return;
			}

			int skip = Mathf.Max(1, _songs.Count - SongContainer.Songs.Count);
			var sortName = GetSortName(song.SongEntry);
			string nextCharacter = GetNextLetterOrNumber(sortName);

			// If an error occurs no change is made
			if (string.IsNullOrEmpty(nextCharacter)) {
				return;
			}

			var index = _songs.FindIndex(skip, song =>
				song is SongViewType songType &&
					GetSortName(songType.SongEntry)[..1] == nextCharacter
				);

			SelectedIndex = index;
		}

		private string GetNextLetterOrNumber(string input){
			if(string.IsNullOrEmpty(input)){
				return null;
			}

			char firstCharacter = char.ToUpper(input[0]);

			int indexOfActualLetter = songsFirstLetter.FindIndex(letter => {
				return letter == firstCharacter;
			});

			bool isLast = indexOfActualLetter == (songsFirstLetter.Count - 1);

			if (isLast) {
				var firstCharacterInList = char.ToString(songsFirstLetter[0]);
				return firstCharacterInList;
			}

			var nextCharacter = char.ToString(songsFirstLetter[indexOfActualLetter + 1]);
			return nextCharacter;
		}

#if UNITY_EDITOR
		public void SetAsTestPlaySong() {
			if (_songs[SelectedIndex] is not SongViewType song) {
				return;
			}

			GameManager.Instance.TestPlayInfo.TestPlaySongHash = song.SongEntry.Checksum;
		}
#endif
	}
}