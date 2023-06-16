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
		public ViewType CurrentSelection => _selectedIndex < _songs.Count ? _songs[_selectedIndex] : null;

		private int _selectedIndex;
		public int SelectedIndex {
			get => _selectedIndex;
			private set {
				SetSelectedIndex(value);
				UpdateScrollbar();
				UpdateSongViews();

				if (CurrentSelection is not SongViewType song) {
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

		private void SetSelectedIndex(int value){
				// Wrap value to bounds
				if (value < 0) {
					_selectedIndex = _songs.Count - 1;
					return;
				}

				if (value >= _songs.Count) {
					_selectedIndex = 0;
					return;
				}

				_selectedIndex = value;
		}

		private List<SongView> _songViews = new();
		private float _scrollTimer = 0f;
		private bool searchBoxShouldBeEnabled = false;
		private readonly int NUMBER_OF_DIVISIONS = 3; //RANDOM SONG, RECOMMENDEND SONGS, ALL SONGS
		private readonly int MIN_SONGS_FOR_SECTION_HEADERS = 30;

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
					CurrentSelection?.PrimaryButtonClick();
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
				return;
			}

			SelectedIndex--;
		}

		private void ScrollDown() {
			if (Navigator.Instance.IsHeld(MenuAction.Shortcut3)) {
				SelectNextSection();
				return;
			}

			SelectedIndex++;
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
			if (CurrentSelection is not SongViewType view) {
				return;
			}

			UpdateFilter();
			UpdateFilterButtonName();
			UpdateNavigationScheme();
		}

		private void UpdateFilter(){
			if (CurrentSelection is not SongViewType view) {
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
			SetScrollTimer();

			if (Keyboard.current.escapeKey.wasPressedThisFrame) {
				ClearSearchBox();
			}

			if (searchBoxShouldBeEnabled) {
				searchField.ActivateInputField();
				searchBoxShouldBeEnabled = false;
			}

			StartPreview();
		}

		private void SetScrollTimer(){
			if (_scrollTimer > 0f) {
				_scrollTimer -= Time.deltaTime;
				return;
			}

			var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

			if (delta > 0f) {
				SelectedIndex--;
				_scrollTimer = SCROLL_TIME;
				return;
			}

			if (delta < 0f) {
				SelectedIndex++;
				_scrollTimer = SCROLL_TIME;
			}
		}

		private void StartPreview(){
			if (!_previewContext.IsPlaying && CurrentSelection is SongViewType song) {
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
			SetRecommendedSongs();

			bool searchBoxHasContent = !string.IsNullOrEmpty(searchField.text);

			if (searchBoxHasContent) {
				IEnumerable<SongEntry> songsOut = SongSearching.Instance.Search(searchField.text);
				AddFilteredSongs(songsOut);
				AddSearchResultsHeader();
			} else {
				AddAllSongs();
				AddSongsCount();
				AddAllReccomendedSongs();
				AddReccommendendSongsHeader();
				AddRandomSongHeader();
			}

			TryToRemoveHeaders();
			SetSelectedIndex();
			UpdateIndex();
			UpdateSongViews();
			UpdateScrollbar();
			AddSectionHeaders();
		}

		private void SetRecommendedSongs(){
			if (_recommendedSongs != null){
				return;
			}

			_recommendedSongs = new();
			
			if (SongContainer.Songs.Count > 0) {
				FillRecommendedSongs();
			}
		}

		private void AddAllSongs(){
			_songs = SongContainer.Songs
				.OrderBy(OrderBy())
				.Select(i => new SongViewType(i))
				.Cast<ViewType>()
				.ToList();
		}

		private void AddSongsCount(){
			var count = _songs.Count;

			_songs.Insert(0, new CategoryViewType(
				"ALL SONGS",
				$"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}",
				SongContainer.Songs
			));
		}

		private void AddAllReccomendedSongs(){
			foreach (var song in _recommendedSongs) {
				_songs.Insert(0, new SongViewType(song));
			}
		}

		private void AddReccommendendSongsHeader(){
			_songs.Insert(0, new CategoryViewType(
				_recommendedSongs.Count == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
				$"<#00B6F5><b>{_recommendedSongs.Count}</b> <#006488>{(_recommendedSongs.Count == 1 ? "SONG" : "SONGS")}",
				_recommendedSongs
			));
		}

		private void AddRandomSongHeader(){
			_songs.Insert(0, new ButtonViewType(
				"RANDOM SONG",
				"Icon/Random",
				() => {
					SelectRandomSong();
				}
			));
		}

		private void AddSectionHeaders() {
			bool sectionHeadersAreVisible = SectionHeadersAreVisible();

			if(!sectionHeadersAreVisible){
				return;
			}

			List<string> sections = SongSorting.Instance.GetSongsFirstLetter();
			int skip = GetSkip();

			foreach (var section in sections) {
				AddSectionHeader(section, skip);
			}
		}

		private bool SectionHeadersAreVisible(){
			return _songs.Count >= MIN_SONGS_FOR_SECTION_HEADERS;
		}

		private void AddSectionHeader(string section, int skip) {
			var index = SongSorting.Instance.GetIndexOfLetter(_songs, section, skip);
			
			_songs.Insert(index, new ButtonViewType(
				$"<#00B6F5><b>{section}</b><#006488>",
				"Icon/ChevronDown",
				() => SelectedIndex++
			));
		}

		private void AddFilteredSongs(IEnumerable<SongEntry> songsOut){
			_songs = songsOut.Select(i => new SongViewType(i)).Cast<ViewType>().ToList();
		}

		private void AddSearchResultsHeader(){
			var count = _songs.Count;
			_songs.Insert(0, new CategoryViewType(
				"SEARCH RESULTS",
				$"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}"
			));
		}

		private void TryToRemoveHeaders(){
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
		}

		private void SetSelectedIndex(){
			if (GameManager.Instance.SelectedSong != null) {
				var index = _songs.FindIndex(song => {
					return song is SongViewType songType && songType.SongEntry == GameManager.Instance.SelectedSong;
				});

				SelectedIndex = Mathf.Max(1, index);
				return;
			}

			var searchBoxHasContent = !string.IsNullOrEmpty(searchField.text);

			if (searchBoxHasContent) {
				SelectedIndex = 1;
				return;
			}

			SelectedIndex = 2;
		}

		private void UpdateIndex(){
			SongSorting.Instance.UpdateIndex(_songs);
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
			bool searchBoxHasContent = !string.IsNullOrEmpty(searchField.text);

			if (searchBoxHasContent) {
				ClearSearchBox();
				UpdateSearch();
				ResetSearchButton();
				UpdateNavigationScheme();
				return;
			}

			if (SelectedIndex > 2) {
				SelectedIndex = 2;
				return;
			}

			MainMenu.Instance.ShowMainMenu();
		}

		private void ClearSearchBox() {
			searchField.text = "";
			searchField.ActivateInputField();
		}

		private void ResetSearchButton(){
			_nextFilter = "Search artist";
		}

		private void SelectRandomSong(){
			int skip = GetSkip();
			// Select random between all of the songs
			SelectedIndex = Random.Range(skip, SongContainer.Songs.Count);
		}

		private void SelectPreviousSection(){
			if (CurrentSelection is not SongViewType song) {
				SelectedIndex--;
				return;
			}

			int firstSongIndex = GetFirstSongIndex();

			if(SongIsRecommendedOrDivision(firstSongIndex)){
				SelectedIndex = GetLastSongIndex(firstSongIndex);
				return;
			}

			SelectedIndex = SongSorting.Instance.SelectNewSection(_songs, SelectedIndex, song, firstSongIndex, SongSorting.PreviousNext.PREVIOUS);
		}

		private void SelectNextSection(){
			if (CurrentSelection is not SongViewType song) {
				SelectedIndex++;
				return;
			}

			int firstSongIndex = GetFirstSongIndex();

			if(SongIsRecommendedOrDivision(firstSongIndex)){
				SelectedIndex = firstSongIndex + 1;//Ad first header
				return;
			}

			SelectedIndex = SongSorting.Instance.SelectNewSection(_songs, SelectedIndex, song, firstSongIndex, SongSorting.PreviousNext.NEXT);
		}

		private bool SongIsRecommendedOrDivision(int skip){
			bool recommendedSongsAreVisible = skip == _recommendedSongs.Count + NUMBER_OF_DIVISIONS;
			return recommendedSongsAreVisible && SelectedIndex < skip;
		}

		private int GetFirstSongIndex(){
			return Mathf.Max(1, _songs.Count - SongContainer.Songs.Count - SongSorting.Instance.GetSectionsSize());
		}

		private int GetLastSongIndex(int skip){
			string lastSection = SongSorting.Instance.GetLastSection();
			return SongSorting.Instance.GetIndexOfLetter(_songs, lastSection, skip);
		}

		private int GetSkip(){
			// Get how many non-song things there are
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
			if (CurrentSelection is not SongViewType song) {
				return;
			}

			GameManager.Instance.TestPlayInfo.TestPlaySongHash = song.SongEntry.Checksum;
		}
#endif
	}
}