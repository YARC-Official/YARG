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
using System.Threading;
using YARG.Settings;

namespace YARG.UI.MusicLibrary {
	public class SongSelection : MonoBehaviour {

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

		private SongSorting.SortCriteria _sortCriteria = SongSorting.SortCriteria.SONG;
		private String _nextSortCriteria = "Order by artist";
		private String _nextFilter = "Search artist";

		private List<ViewType> _songs;
		private List<SongEntry> _recommendedSongs;

		private PreviewContext _previewContext;
		private CancellationTokenSource _previewCanceller = new();

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

				if (!_previewCanceller.IsCancellationRequested) {
					_previewCanceller.Cancel();
				}
			}
		}

		private List<SongView> _songViews = new();
		private float _scrollTimer = 0f;
		private bool searchBoxShouldBeEnabled = false;
		private readonly int numberOfDivisions = 3; //RANDOM SONG, RECOMMENDEND SONGS, ALL SONGS

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
			var navigationScheme = GetNavigationScheme();
			Navigator.Instance.PushScheme(navigationScheme);

			if (refreshFlag) {
				_songs = null;
				_recommendedSongs = null;

				// Get songs
				UpdateSearch();
				refreshFlag = false;
			}

			searchBoxShouldBeEnabled = true;
		}

		private NavigationScheme GetNavigationScheme(){
			return new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Up, "Up", () => {
					ScrollUp();
				}),
				new NavigationScheme.Entry(MenuAction.Down, "Down", () => {
					ScrollDown();
				}),
				new NavigationScheme.Entry(MenuAction.Confirm, "Confirm", () => {
					_songs[SelectedIndex]?.PrimaryButtonClick();
				}),
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => {
					Back();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut1, _nextSortCriteria, () => {
					ChangeSongOrder();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut2, _nextFilter, () => {
					ChangeFilter();
				}),
				new NavigationScheme.Entry(MenuAction.Shortcut3, "(Hold) Section", () => {})
			}, false);
		}

		private void ScrollUp() {
			if (Navigator.Instance.IsHeld(MenuAction.Shortcut3)) {
				SelectPreviousSection();
			} else {
				SelectedIndex--;
			}
		}

		private void ScrollDown() {
			if (Navigator.Instance.IsHeld(MenuAction.Shortcut3)) {
				SelectNextSection();
			} else {
				SelectedIndex++;
			}
		}

		private void OnDisable() {
			Navigator.Instance.PopScheme();

			if (!_previewCanceller.IsCancellationRequested) {
				_previewCanceller.Cancel();
			}

			_previewContext = null;
		}

		private void UpdateSongViews() {
			foreach (var songView in _songViews) {
				songView.UpdateView();
			}

			sidebar.UpdateSidebar().Forget();
		}

		private void ChangeSongOrder() {
			UpdateSortLamda();
			UpdateSearch();
			UpdateNextSortCriteria();
			UpdateNavigationScheme();
		}

		public void UpdateSortLamda(){
			_sortCriteria = getNextSortCriteria();
			SongSorting.Instance.OrderBy(_sortCriteria);
		}

		public SongSorting.SortCriteria getNextSortCriteria() {
			return SongSorting.Instance.GetNextSortCriteria(_sortCriteria);
		}

		private void UpdateNextSortCriteria(){
			_nextSortCriteria = GetNextSortCriteriaButtonName();
		}

		private void UpdateNavigationScheme(){
			Navigator.Instance.PopScheme();
			Navigator.Instance.PushScheme(GetNavigationScheme());
		}

		private void ChangeFilter() {
			UpdateFilter();
			UpdateFilterButtonName();
			UpdateNavigationScheme();
		}

		private void UpdateFilter(){
			if (_songs[SelectedIndex] is not SongViewType view) {
				return;
			}

			var text = searchField.text;

			if (string.IsNullOrEmpty(text) || text.StartsWith("source:")) {
				var artist = view.SongEntry.Artist;
				searchField.text = $"artist:{artist}";
				return;
			}

			if (text.StartsWith("artist:")) {
				var source = view.SongEntry.Source;
				searchField.text = $"source:{source}";
				return;
			}
		}

		private void UpdateFilterButtonName(){
			_nextFilter = _nextFilter switch {
				"Search artist" => "Search source",
				"Search source" => "Search artist",
				_ => "Search artist"
			};
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

			// Start preview
			if (!_previewContext.IsPlaying && _songs[SelectedIndex] is SongViewType song) {
				_previewCanceller = new();
				float previewVolume = SettingsManager.Settings.PreviewVolume.Data;
				_previewContext.PlayPreview(song.SongEntry, previewVolume, _previewCanceller.Token).Forget();
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
					.OrderBy(OrderBy())
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
							.Where(i => RemoveDiacriticsAndArticle(i.Artist) == RemoveDiacriticsAndArticle(artist));
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
					// songsOut = songsOut.OrderBy(song => GetSortName(song));
					songsOut = songsOut.OrderBy(OrderBy());
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

			UpdateIndex();
			UpdateSongViews();
			UpdateScrollbar();
		}

		private void UpdateIndex(){
			SongSorting.Instance.UpdateIndex(_songs);
		}

		private static string RemoveDiacriticsAndArticle(string text){
			var textWithoutDiacretics = RemoveDiacritics(text);
			return SongSorting.RemoveArticle(textWithoutDiacretics);
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

		private void SelectPreviousSection(){
			if (_songs[_selectedIndex] is not SongViewType song) {
				return;
			}

			int skip = GetSkip();

			if(SongIsRecommendedOrDivision(skip)){
				SelectedIndex = 1;
				return;
			}

			SelectedIndex = SongSorting.Instance.SelectNewSection(_songs, SelectedIndex, song, skip, SongSorting.PreviousNext.PREVIOUS);
		}

		private void SelectNextSection(){
			if (_songs[_selectedIndex] is not SongViewType song) {
				return;
			}

			int skip = GetSkip();

			if(SongIsRecommendedOrDivision(skip)){
				SelectedIndex = skip;
				return;
			}

			SelectedIndex = SongSorting.Instance.SelectNewSection(_songs, SelectedIndex, song, skip, SongSorting.PreviousNext.NEXT);
		}

		private bool SongIsRecommendedOrDivision(int skip){
			bool recommendedSongsAreVisible = skip == _recommendedSongs.Count + numberOfDivisions;
			return recommendedSongsAreVisible && SelectedIndex < skip;
		}

		private int GetSkip(){
			return Mathf.Max(1, _songs.Count - SongContainer.Songs.Count);
		}

		private string GetNextSortCriteriaButtonName() {
			return SongSorting.Instance.GetNextSortCriteriaButtonName(_sortCriteria);
		}

		private Func<SongEntry, string> OrderBy(){
			return SongSorting.Instance.SortBy();
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