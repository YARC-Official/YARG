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
using UnityEngine.Serialization;
using YARG.Settings;

namespace YARG.UI.MusicLibrary
{
    public class SongSelection : MonoBehaviour
    {
        public static SongSelection Instance { get; private set; }

        public static bool RefreshFlag = true;

        private const int SONG_VIEW_EXTRA = 15;
        private const float SCROLL_TIME = 1f / 60f;

        [SerializeField]
        private GameObject _songViewPrefab;

        [Space]
        [SerializeField]
        private TMP_InputField _searchField;

        [SerializeField]
        private Transform _songListContent;

        [SerializeField]
        private Sidebar _sidebar;

        [SerializeField]
        private Scrollbar _scrollbar;

        private SongSorting.Sort _sort = SongSorting.Sort.Song;
        private string _nextSortCriteria = "Order by artist";
        private string _nextFilter = "Search artist";

        private List<ViewType> _viewList;
        private List<SongView> _songViewObjects;

        private SortedSongList _sortedSongs;
        private List<SongEntry> _recommendedSongs;

        private PreviewContext _previewContext;
        private CancellationTokenSource _previewCanceller = new();

        public IReadOnlyList<ViewType> ViewList => _viewList;
        public ViewType CurrentSelection => _selectedIndex < _viewList?.Count ? _viewList[_selectedIndex] : null;

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            private set
            {
                SetSelectedIndex(value);
                UpdateScrollbar();
                UpdateSongViews();

                if (CurrentSelection is not SongViewType song)
                {
                    return;
                }

                if (song.SongEntry == GameManager.Instance.SelectedSong)
                {
                    return;
                }

                GameManager.Instance.SelectedSong = song.SongEntry;

                if (!_previewCanceller.IsCancellationRequested)
                {
                    _previewCanceller.Cancel();
                }
            }
        }

        private float _scrollTimer = 0f;
        private bool _searchBoxShouldBeEnabled = false;

        private void Awake()
        {
            RefreshFlag = true;
            Instance = this;

            // Create all of the song views
            _songViewObjects = new();
            for (int i = 0; i < SONG_VIEW_EXTRA * 2 + 1; i++)
            {
                var gameObject = Instantiate(_songViewPrefab, _songListContent);

                // Init and add
                var songView = gameObject.GetComponent<SongView>();
                songView.Init(i - SONG_VIEW_EXTRA);
                _songViewObjects.Add(songView);
            }

            // Initialize sidebar
            _sidebar.Init();
        }

        private void OnEnable()
        {
            // Set up preview context
            _previewContext = new(GameManager.AudioManager);

            // Set navigation scheme
            var navigationScheme = GetNavigationScheme();
            Navigator.Instance.PushScheme(navigationScheme);

            if (RefreshFlag)
            {
                _viewList = null;
                _recommendedSongs = null;

                // Get songs
                UpdateSearch();
                RefreshFlag = false;
            }

            _searchBoxShouldBeEnabled = true;
        }

        private void SetSelectedIndex(int value)
        {
            // Wrap value to bounds
            if (value < 0)
            {
                _selectedIndex = _viewList.Count - 1;
                return;
            }

            if (value >= _viewList.Count)
            {
                _selectedIndex = 0;
                return;
            }

            _selectedIndex = value;
        }

        public void SetSearchInput(string query)
        {
            _searchField.text = query;
        }

        private NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Up", ScrollUp),
                new NavigationScheme.Entry(MenuAction.Down, "Down", ScrollDown),
                new NavigationScheme.Entry(MenuAction.Confirm, "Confirm",
                    () => { CurrentSelection?.PrimaryButtonClick(); }),
                new NavigationScheme.Entry(MenuAction.Back, "Back", Back),
                new NavigationScheme.Entry(MenuAction.Shortcut1, _nextSortCriteria, ChangeSongOrder),
                new NavigationScheme.Entry(MenuAction.Shortcut2, _nextFilter, ChangeFilter),
                new NavigationScheme.Entry(MenuAction.Shortcut3, "(Hold) Section", () => { })
            }, false);
        }

        private void ScrollUp()
        {
            if (Navigator.Instance.IsHeld(MenuAction.Shortcut3))
            {
                SelectPreviousSection();
                return;
            }

            SelectedIndex--;
        }

        private void ScrollDown()
        {
            if (Navigator.Instance.IsHeld(MenuAction.Shortcut3))
            {
                SelectNextSection();
                return;
            }

            SelectedIndex++;
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();

            if (!_previewCanceller.IsCancellationRequested)
            {
                _previewCanceller.Cancel();
            }

            _previewContext = null;
        }

        private void UpdateSongViews()
        {
            foreach (var songView in _songViewObjects)
            {
                songView.UpdateView();
            }

            _sidebar.UpdateSidebar().Forget();
        }

        private void ChangeSongOrder()
        {
            NextSort();

            UpdateSearch();
            UpdateNavigationScheme();
        }

        public void NextSort()
        {
            _sort = SongSorting.GetNextSortCriteria(_sort);
            _nextSortCriteria = GetNextSortCriteriaButtonName();
        }

        private void UpdateNavigationScheme()
        {
            Navigator.Instance.PopScheme();
            Navigator.Instance.PushScheme(GetNavigationScheme());
        }

        private void ChangeFilter()
        {
            if (CurrentSelection is not SongViewType)
            {
                return;
            }

            UpdateFilter();
            UpdateFilterButtonName();
            UpdateNavigationScheme();
        }

        private void UpdateFilter()
        {
            if (CurrentSelection is not SongViewType view)
            {
                return;
            }

            var text = _searchField.text;

            if (string.IsNullOrEmpty(text) || text.StartsWith("source:"))
            {
                var artist = view.SongEntry.Artist;
                _searchField.text = $"artist:{artist}";
                return;
            }

            if (text.StartsWith("artist:"))
            {
                var source = view.SongEntry.Source;
                _searchField.text = $"source:{source}";
            }
        }

        private void UpdateFilterButtonName()
        {
            _nextFilter = _nextFilter switch
            {
                "Search artist" => "Search source",
                "Search source" => "Search artist",
                _               => "Search artist"
            };
        }

        private void Update()
        {
            SetScrollTimer();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClearSearchBox();
            }

            if (_searchBoxShouldBeEnabled)
            {
                _searchField.ActivateInputField();
                _searchBoxShouldBeEnabled = false;
            }

            StartPreview();
        }

        private void SetScrollTimer()
        {
            if (_scrollTimer > 0f)
            {
                _scrollTimer -= Time.deltaTime;
                return;
            }

            var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

            if (delta > 0f)
            {
                SelectedIndex--;
                _scrollTimer = SCROLL_TIME;
                return;
            }

            if (delta < 0f)
            {
                SelectedIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }

        private void StartPreview()
        {
            if (!_previewContext.IsPlaying && CurrentSelection is SongViewType song)
            {
                _previewCanceller = new();
                float previewVolume = SettingsManager.Settings.PreviewVolume.Data;
                _previewContext.PlayPreview(song.SongEntry, previewVolume, _previewCanceller.Token).Forget();
            }
        }

        public void OnScrollBarChange()
        {
            SelectedIndex = Mathf.FloorToInt(_scrollbar.value * (_viewList.Count - 1));
        }

        private void UpdateScrollbar()
        {
            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / _viewList.Count);
        }

        public void UpdateSearch()
        {
            SetRecommendedSongs();

            _sortedSongs = SongSearching.Search(_searchField.text, _sort);

            AddSongs();

            if (!string.IsNullOrEmpty(_searchField.text))
            {
                GameManager.Instance.SelectedSong = null;

                // Create the category
                int count = _sortedSongs.SongCount();
                var categoryView = new CategoryViewType(
                    "SEARCH RESULTS",
                    $"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}"
                );

                if (_sortedSongs.SectionNames.Count == 1)
                {
                    // If there is only one header, just replace it
                    _viewList[0] = categoryView;
                }
                else
                {
                    // Otherwise add to top
                    _viewList.Insert(0, categoryView);
                }
            }
            else
            {
                AddSongsCount();
                AddAllRecommendedSongs();
                AddRecommendSongsHeader();
                AddRandomSongHeader();
            }

            ClearIfNoSongs();

            SetSelectedIndex();
            // These are both called by the above:
            // UpdateSongViews();
            // UpdateScrollbar();
        }

        private void AddSongs()
        {
            _viewList = new();

            foreach (var section in _sortedSongs.SectionNames)
            {
                var songs = _sortedSongs.SongsInSection(section);

                // Create header
                _viewList.Add(new SortHeaderViewType(section, songs.Count));

                // Add all of the songs
                foreach (var song in songs)
                {
                    _viewList.Add(new SongViewType(song));
                }
            }
        }

        private void SetRecommendedSongs()
        {
            if (_recommendedSongs != null)
            {
                return;
            }

            _recommendedSongs = new();

            if (SongContainer.Songs.Count > 0)
            {
                FillRecommendedSongs();
            }
        }

        private void AddSongsCount()
        {
            var count = _viewList.Count;

            _viewList.Insert(0, new CategoryViewType(
                "ALL SONGS",
                $"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}",
                SongContainer.Songs
            ));
        }

        private void AddAllRecommendedSongs()
        {
            foreach (var song in _recommendedSongs)
            {
                _viewList.Insert(0, new SongViewType(song));
            }
        }

        private void AddRecommendSongsHeader()
        {
            _viewList.Insert(0, new CategoryViewType(
                _recommendedSongs.Count == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
                $"<#00B6F5><b>{_recommendedSongs.Count}</b> <#006488>{(_recommendedSongs.Count == 1 ? "SONG" : "SONGS")}",
                _recommendedSongs
            ));
        }

        private void AddRandomSongHeader()
        {
            _viewList.Insert(0, new ButtonViewType(
                "RANDOM SONG",
                "Icon/Random",
                SelectRandomSong
            ));
        }

        private void ClearIfNoSongs()
        {
            // Count songs
            int songCount = _viewList.OfType<SongViewType>().Count();

            // If there are no songs, remove the headers
            if (songCount <= 0)
            {
                _viewList.Clear();
            }
        }

        private void SetSelectedIndex()
        {
            if (GameManager.Instance.SelectedSong != null)
            {
                int index = GetIndexOfSelectedSong();
                SelectedIndex = Mathf.Max(1, index);
                return;
            }

            if (!string.IsNullOrEmpty(_searchField.text))
            {
                SelectedIndex = 1;
                return;
            }

            SelectedIndex = 2;
        }

        private int GetIndexOfSelectedSong()
        {
            var selectedSong = GameManager.Instance.SelectedSong;

            return _viewList.FindIndex(song =>
            {
                return song is SongViewType songType && songType.SongEntry == selectedSong;
            });
        }

        private void FillRecommendedSongs()
        {
            _recommendedSongs = RecommendedSongs.GetRecommendedSongs();
        }

        public void Back()
        {
            bool searchBoxHasContent = !string.IsNullOrEmpty(_searchField.text);

            if (searchBoxHasContent)
            {
                ClearSearchBox();
                UpdateSearch();
                ResetSearchButton();
                UpdateNavigationScheme();
                return;
            }

            if (SelectedIndex > 2)
            {
                SelectedIndex = 2;
                return;
            }

            MainMenu.Instance.ShowMainMenu();
        }

        private void ClearSearchBox()
        {
            _searchField.text = "";
            _searchField.ActivateInputField();
        }

        private void ResetSearchButton()
        {
            _nextFilter = "Search artist";
        }

        private void SelectRandomSong()
        {
            int skip = GetSkip();

            // Select random between all of the songs
            SelectedIndex = Random.Range(skip, SongContainer.Songs.Count);
        }

        public void SelectPreviousSection()
        {
            SelectedIndex = _viewList.FindLastIndex(SelectedIndex - 1, i => i is SortHeaderViewType);

            // Wrap back around
            if (SelectedIndex == _viewList.Count - 1)
            {
                SelectedIndex = _viewList.FindLastIndex(i => i is SortHeaderViewType);
            }
        }

        public void SelectNextSection()
        {
            SelectedIndex = _viewList.FindIndex(SelectedIndex + 1, i => i is SortHeaderViewType);

            // Wrap back around to recommended
            if (SelectedIndex == _viewList.Count - 1)
            {
                SelectedIndex = _viewList.FindIndex(i => i is SortHeaderViewType);
            }
        }

        private int GetSkip()
        {
            // Get how many non-song things there are
            return Mathf.Max(1, _viewList.Count - SongContainer.Songs.Count);
        }

        private string GetNextSortCriteriaButtonName()
        {
            return SongSorting.GetNextSortButtonName(_sort);
        }

#if UNITY_EDITOR
        public void SetAsTestPlaySong()
        {
            if (CurrentSelection is not SongViewType song)
            {
                return;
            }

            GameManager.Instance.TestPlayInfo.TestPlaySongHash = song.SongEntry.Checksum;
        }
#endif
    }
}