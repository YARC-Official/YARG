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

		public static bool RefreshFlag = true;

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

		private SongSorting.Sort _sort = SongSorting.Sort.Song;
		private string _nextSortCriteria = "Order by artist";
		private string _nextFilter = "Search artist";

		private SortedSongList _sortedSongs;

		private List<ViewType> _songs;
		private List<SongEntry> _recommendedSongs;

		private PreviewContext _previewContext;
		private CancellationTokenSource _previewCanceller = new();

		public IReadOnlyList<ViewType> Songs => _songs;
		public ViewType CurrentSelection => _selectedIndex < _songs?.Count ? _songs[_selectedIndex] : null;

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
			RefreshFlag = true;
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

			if (RefreshFlag) {
				_songs = null;
				_recommendedSongs = null;

				// Get songs
				UpdateSearch();
				RefreshFlag = false;
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
			NextSort();

			UpdateSearch();
			UpdateNavigationScheme();
		}

		public void NextSort(){
			_sort = SongSorting.GetNextSortCriteria(_sort);
			_nextSortCriteria = GetNextSortCriteriaButtonName();
		}

		private void UpdateNavigationScheme(){
			Navigator.Instance.PopScheme();
			Navigator.Instance.PushScheme(GetNavigationScheme());
		}

		private void ChangeFilter() {
			if (CurrentSelection is not SongViewType) {
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
			_sortedSongs = SongSearching.Search(searchField.text, _sort);

			AddSongs();

			if (!string.IsNullOrEmpty(searchField.text)) {
				AddSearchResultsHeader();
			} else {
				AddSongsCount();
				AddAllRecommendedSongs();
				AddRecommendSongsHeader();
				AddRandomSongHeader();
			}

			TryToRemoveHeaders();
			UpdateSongViews();
			UpdateScrollbar();
			SetSelectedIndex();
		}

		private void AddSongs() {
			_songs = new();

			foreach (var sectionObj in _sortedSongs.SectionsCollection()) {
				var section = (string) sectionObj;

				// Create header
				_songs.Add(new SortHeaderViewType(
					$"<#00B6F5><b>{section}</b><#006488>",
					"Icon/ChevronDown",
					() => SelectedIndex++
				));

				// Add all of the songs
				foreach (var song in _sortedSongs.SongsInSection(section)) {
					_songs.Add(new SongViewType(song));
				}
			}
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

		private void AddSongsCount(){
			var count = _songs.Count;

			_songs.Insert(0, new CategoryViewType(
				"ALL SONGS",
				$"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}",
				SongContainer.Songs
			));
		}

		private void AddAllRecommendedSongs(){
			foreach (var song in _recommendedSongs) {
				_songs.Insert(0, new SongViewType(song));
			}
		}

		private void AddRecommendSongsHeader(){
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
				int index = GetIndexOfSelectedSong();
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

		private int GetIndexOfSelectedSong(){
			var selectedSong = GameManager.Instance.SelectedSong;

			return _songs.FindIndex(song => {
				return song is SongViewType songType && songType.SongEntry == selectedSong;
			});
		}

		private void FillRecommendedSongs() {
			_recommendedSongs = RecommendedSongs.Instance.GetRecommendedSongs();
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
			if (CurrentSelection is not SongViewType) {
				SelectedIndex--;
				return;
			}

			SelectedIndex = _songs.FindLastIndex(0, SelectedIndex,
				i => i is SortHeaderViewType);

			// Wrap back around
			if (SelectedIndex == -1) {
				SelectedIndex = _songs.FindLastIndex(i => i is SortHeaderViewType);
			}
		}

		private void SelectNextSection(){
			if (CurrentSelection is not SongViewType) {
				SelectedIndex++;
				return;
			}

			SelectedIndex = _songs.FindIndex(SelectedIndex, i => i is SortHeaderViewType);

			// Wrap back around to recommended
			if (SelectedIndex == -1) {
				SelectedIndex = _songs.FindIndex(i => i is SortHeaderViewType);
			}
		}

		private int GetSkip(){
			// Get how many non-song things there are
			return Mathf.Max(1, _songs.Count - SongContainer.Songs.Count);
		}

		private string GetNextSortCriteriaButtonName() {
			return SongSorting.GetNextSortButtonName(_sort);
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