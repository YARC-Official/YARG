using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

using Random = UnityEngine.Random;

namespace YARG.Menu.MusicLibrary
{
    public enum MusicLibraryMode
    {
        QuickPlay,
        Practice
    }

    public class MusicLibraryMenu : MonoSingleton<MusicLibraryMenu>
    {
        private const int SONG_VIEW_EXTRA = 15;
        private const float SCROLL_TIME = 1f / 60f;

        public static bool RefreshFlag = true;

        [HideInInspector]
        public MusicLibraryMode LibraryMode;

        [SerializeField]
        private GameObject _songViewPrefab;

        [Space]
        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Transform _songListContent;
        [SerializeField]
        private Sidebar _sidebar;
        [SerializeField]
        private Scrollbar _scrollbar;
        [SerializeField]
        private GameObject _noPlayerWarning;

        private static SongAttribute _sort = SongAttribute.Name;
        private string _nextSortCriteria = "Order by artist";

        private List<ViewType> _viewList;
        private List<SongView> _songViewObjects;

        private SongSearching _searchBar = new();
        private SortedDictionary<string, List<SongMetadata>> _sortedSongs;
        private List<SongMetadata> _recommendedSongs;

        private PreviewContext _previewContext;
        private CancellationTokenSource _previewCanceller = new();

        public IReadOnlyList<ViewType> ViewList => _viewList;
        public ViewType CurrentSelection => 0 <= _selectedIndex && _selectedIndex < _viewList?.Count ? _viewList[_selectedIndex] : null;

        private SongMetadata _currentSong;

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            private set
            {

                // Properly wrap the value
                if (value < 0)
                {
                    _selectedIndex = _viewList.Count - 1;
                }
                else if (value >= _viewList.Count)
                {
                    _selectedIndex = 0;
                }
                else
                {
                    _selectedIndex = value;
                }

                UpdateScrollbar();
                UpdateSongViews();

                if (CurrentSelection is not SongViewType song)
                {
                    return;
                }

                if (song.SongEntry == _currentSong)
                {
                    return;
                }

                _currentSong = song.SongEntry;

                if (!_previewCanceller.IsCancellationRequested)
                {
                    _previewCanceller.Cancel();
                }
            }
        }

        private float _scrollTimer = 0f;
        private bool _searchBoxShouldBeEnabled = false;

        protected override void SingletonAwake()
        {
            RefreshFlag = true;

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
            _previewContext = new(GlobalVariables.AudioManager);

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

            // Set proper text
            _subHeader.text = LibraryMode switch
            {
                MusicLibraryMode.QuickPlay => "Quickplay",
                MusicLibraryMode.Practice  => "Practice",
                _ => throw new Exception("Unreachable.")
            };

            // Set IsPractice as well
            GlobalVariables.Instance.IsPractice = LibraryMode == MusicLibraryMode.Practice;

            // Show no player warning
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);

            _searchBoxShouldBeEnabled = true;
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

        private void Update()
        {
            UpdateScroll();

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
                new NavigationScheme.Entry(MenuAction.Green, "Confirm", Confirm),
                new NavigationScheme.Entry(MenuAction.Red, "Back", Back),
                new NavigationScheme.Entry(MenuAction.Yellow, _nextSortCriteria, ChangeSongOrder),
                new NavigationScheme.Entry(MenuAction.Blue, "(Hold) Section", () => { })
            }, false);
        }

        private void ScrollUp()
        {
            if (Navigator.Instance.IsHeld(MenuAction.Orange))
            {
                SelectPreviousSection();
                return;
            }

            SelectedIndex--;
        }

        private void ScrollDown()
        {
            if (Navigator.Instance.IsHeld(MenuAction.Orange))
            {
                SelectNextSection();
                return;
            }

            SelectedIndex++;
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
            var next = (int) _sort + 1;
            if (next >= Enum.GetNames(typeof(SongAttribute)).Length)
            {
                next = 1;
            }
            _sort = (SongAttribute) next;

            SetNextSortCriteria();
        }

        private void SetNextSortCriteria()
        {
            _nextSortCriteria = _sort switch
            {
                SongAttribute.Name => "Order by Artist",
                SongAttribute.Artist => "Order by Album",
                SongAttribute.Album => "Order by \"Artist - Album\"",
                SongAttribute.Artist_Album => "Order by Genre",
                SongAttribute.Genre => "Order by Year",
                SongAttribute.Year => "Order by Charter",
                SongAttribute.Charter => "Order by Playlist",
                SongAttribute.Playlist => "Order by Source",
                SongAttribute.Source => "Order by Duration",
                SongAttribute.SongLength => "Order by Song",
                _ => "Order by Song"
            };
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

            UpdateNavigationScheme();
        }

        private void UpdateScroll()
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

            _sortedSongs = _searchBar.Search(_searchField.text, _sort);

            AddSongs();

            if (!string.IsNullOrEmpty(_searchField.text))
            {
                // Create the category
                int count = 0;
                foreach (var section in _sortedSongs)
                    count += section.Value.Count;

                var categoryView = new CategoryViewType(
                    "SEARCH RESULTS",
                    $"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}"
                );

                if (_sortedSongs.Count == 1)
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
            else if (GlobalVariables.Instance.SongContainer.Count > 0)
            {
                AddSongsCount();
                AddAllRecommendedSongs();
                AddRecommendSongsHeader();
                AddRandomSongHeader();
                ClearIfNoSongs();
            }
            else
            {
                _viewList.Clear();
                UpdateSongViews();
                return;
            }

            SetSelectedIndex();
            // These are both called by the above:
            // UpdateSongViews();
            // UpdateScrollbar();
        }

        private void AddSongs()
        {
            _viewList = new();

            foreach (var section in _sortedSongs)
            {
                // Create header
                _viewList.Add(new SortHeaderViewType(section.Key, section.Value.Count));

                // Add all of the songs
                foreach (var song in section.Value)
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

            if (GlobalVariables.Instance.SongContainer.Songs.Count > 0)
            {
                FillRecommendedSongs();
            }
        }

        private void AddSongsCount()
        {
            var count = GlobalVariables.Instance.SongContainer.Count;

            _viewList.Insert(0, new CategoryViewType(
                "ALL SONGS",
                $"<#00B6F5><b>{count}</b> <#006488>{(count == 1 ? "SONG" : "SONGS")}",
                GlobalVariables.Instance.SongContainer.Songs
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
            if (_currentSong != null)
            {
                int index = GetIndexOfSelectedSong();
                if (index >= 0)
                {
                    SelectedIndex = Mathf.Max(1, index);
                    return;
                }
                else
                    _currentSong = null;
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
            var selectedSong = _currentSong;

            // Get the first index after the recommended songs
            int startOfSongs = _viewList.FindIndex(i => i is SortHeaderViewType || i is CategoryViewType);
            if (startOfSongs < 0)
                return -1;

            int songIndex = _viewList.FindIndex(startOfSongs,
                song => song is SongViewType songType && songType.SongEntry == selectedSong);

            return songIndex;
        }

        private void FillRecommendedSongs()
        {
            _recommendedSongs = RecommendedSongs.GetRecommendedSongs();
        }

        private void Confirm()
        {
            CurrentSelection?.PrimaryButtonClick();
        }

        private void Back()
        {
            bool searchBoxHasContent = !string.IsNullOrEmpty(_searchField.text);

            if (searchBoxHasContent)
            {
                ClearSearchBox();
                UpdateSearch();
                UpdateNavigationScheme();
                return;
            }

            MenuManager.Instance.PopMenu();
        }

        private void ClearSearchBox()
        {
            _searchField.text = "";
            _searchField.ActivateInputField();
        }

        private void SelectRandomSong()
        {
            int skip = GetSkip();

            // Select random between all of the songs
            SelectedIndex = Random.Range(skip, GlobalVariables.Instance.SongContainer.Songs.Count);
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
            return Mathf.Max(1, _viewList.Count - GlobalVariables.Instance.SongContainer.Songs.Count);
        }
    }
}