using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Playlists;
using YARG.Settings;
using YARG.Song;
using static YARG.Menu.Navigation.Navigator;
using Random = UnityEngine.Random;

namespace YARG.Menu.MusicLibrary
{
    public enum MusicLibraryMode
    {
        QuickPlay,
        Practice
    }

    public enum MusicLibraryReloadState
    {
        None,
        Partial,
        Full
    }

    public enum MenuState
    {
        Library,
        PlaylistSelect,
        Playlist,
        Show
    }

    public partial class MusicLibraryMenu : ListMenu<ViewType, SongView>
    {
        private const int RANDOM_SONG_ID = 0;
        private const int PLAYLIST_ID = 1;
        private const int BACK_ID = 2;

        public static MusicLibraryMode LibraryMode;

        public static SongEntry CurrentlyPlaying;
        public        MenuState MenuState;
        public        Playlist  SelectedPlaylist;

#nullable enable
        private static SongEntry[]? _recommendedSongs;
#nullable disable

        private static string                  _currentSearch = string.Empty;
        private static int                     _savedIndex;
        private static SelectionSnapshot       _savedSelectionSnapshot;
        private static bool                    _hasSavedSelectionSnapshot;
        private static bool                    _preferHeaderOnNextSnapshot;
        private static int                     _mainLibraryIndex = -1;
        private static MusicLibraryReloadState _reloadState = MusicLibraryReloadState.Full;
        private static Playlist                _savedPlaylist;

        public bool PlaylistMode => SelectedPlaylist != null;

        public static void SetReload(MusicLibraryReloadState state)
        {
            _reloadState = state;
        }

        [Space]
        [SerializeField]
        private SongSearchingField _searchField;
        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Sidebar _sidebar;
        [SerializeField]
        private GameObject _noPlayerWarning;
        [SerializeField]
        private PopupMenu _popupMenu;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _sortInfoHeaderPrimaryText;
        [SerializeField]
        private TextMeshProUGUI _sortInfoHeaderSongCountText;
        [SerializeField]
        private TextMeshProUGUI _sortInfoHeaderStarCountText;
        [SerializeField]
        private Image _sortInfoHeaderStarIcon;
        private int _totalSongCount = 0;
        private int _totalSongCountUnfiltered = 0;
        private int _totalStarCount = 0;
        private int _numPlaylists = 0;

        protected override int ExtraListViewPadding => 15;
        protected override bool CanScroll => !_popupMenu.gameObject.activeSelf;

        public bool HasSortHeaders { get; private set; }

        public bool ShouldDisplaySoloHighScores { get; private set; }

        private SongCategory[] _sortedSongs;

        private CancellationTokenSource _previewCanceller;
        private PreviewContext _previewContext;
        private double _previewDelay;

        private SongEntry _currentSong;

        private List<int> _sectionHeaderIndices = new();
        public List<(string, int)> Shortcuts { get; private set; } = new();

        private List<HoldContext> _heldInputs = new();

        // Doesn't go through PlaylistContainer because it is ephemeral

        private static Instrument _lastInstrument;
        private static Difficulty _lastDifficulty;

        private static bool _needsReload = false;

        public static void NeedsReload()
        {
            _needsReload = true;
        }

        private int _primaryHeaderIndex;

        protected override void Awake()
        {
            base.Awake();

            // Initialize sidebar
            _sidebar.Initialize(this, _searchField);

            // Fill in sort information
            UpdateSortInformationHeader();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Set navigation scheme
            SetNavigationScheme();

            // Restore search
            _searchField.Restore();
            _searchField.OnSearchQueryUpdated += UpdateSearch;

            if (CurrentlyPlaying != null)
            {
                _currentSong = CurrentlyPlaying;
            }

            SetRefreshIfNeeded();

            StemSettings.ApplySettings = SettingsManager.Settings.ApplyVolumesInMusicLibrary.Value;
            _previewDelay = 0;
            if (_reloadState == MusicLibraryReloadState.Full)
            {
                Refresh();
            }
            else if (_reloadState == MusicLibraryReloadState.Partial)
            {
                // Note that the order matters here: SelectedPlaylist must be set before calling UpdateSearch,
                // but SelectedIndex must be set _after_ calling UpdateSearch
                SelectedPlaylist = _savedPlaylist;
                if (SelectedPlaylist != null)
                {
                    // Preserve the playlist select anchor across menu reloads (e.g., after playing a song)
                    _lastPlaylistSelectPlaylist = SelectedPlaylist;
                    MenuState = MenuState.Playlist;
                }

                UpdateSearch(true);

                if (MenuState == MenuState.Library && _mainLibraryIndex != -1)
                {
                    SelectedIndex = _mainLibraryIndex;
                }
                else
                {
                    SelectedIndex = _savedIndex;
                }
            }
            else if (_currentSong != null)
            {
                UpdateSearch(true);
            }

            if (MenuState == MenuState.Library && _hasSavedSelectionSnapshot)
            {
                RestoreSelectionSnapshot(_savedSelectionSnapshot);
                _hasSavedSelectionSnapshot = false;
            }

            CurrentlyPlaying = null;
            _reloadState = MusicLibraryReloadState.None;

            // Set proper text
            _subHeader.text = LibraryMode switch
            {
                MusicLibraryMode.QuickPlay => Localize.Key("Menu.Main.Options.Quickplay"),
                MusicLibraryMode.Practice  => Localize.Key("Menu.Main.Options.Practice"),
                _                          => throw new Exception("Unreachable.")
            };

            // Set IsPractice as well
            GlobalVariables.State.IsPractice = LibraryMode == MusicLibraryMode.Practice;
            GlobalVariables.State.CurrentReplay = null;
            GlobalVariables.State.PlayingWithReplay = false;

            // Show no player warning
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);

            // Make sure sort is not by play count if there are only bots
            if (PlayerContainer.OnlyHasBotsActive() &&
                (SettingsManager.Settings.LibrarySort == SortAttribute.Playcount ||
                    SettingsManager.Settings.LibrarySort == SortAttribute.Stars))
            {
                // Name makes a good fallback?
                ChangeSort(SortAttribute.Name);
            }

            // Fill in sort information
            UpdateSortInformationHeader();

            PlayerContainer.PlayerAdded += OnPlayerAdded;
            PlayerContainer.PlayerRemoved += OnPlayerRemoved;

            // Ensure the sidebar is rendered correctly on first entry
            _sidebar.UpdateSidebar(true);
        }

        private void SetRefreshIfNeeded()
        {
            YargProfile profile = null;
            foreach (YargPlayer p in PlayerContainer.Players)
            {
                if (!p.Profile.IsBot)
                {
                    profile = p.Profile;
                    break;
                }
            }
            Instrument currentInstrument = profile?.CurrentInstrument ?? Instrument.FiveFretGuitar;
            Difficulty currentDifficulty = profile?.CurrentDifficulty ?? Difficulty.Expert;
            if (_needsReload ||
                currentInstrument != _lastInstrument ||
                currentDifficulty != _lastDifficulty)
            {
                _lastInstrument = currentInstrument;
                _lastDifficulty = currentDifficulty;
                _needsReload = false;

                if (_reloadState != MusicLibraryReloadState.Full)
                {
                    _reloadState = MusicLibraryReloadState.Partial;
                }
            }
        }

        // Public because PopupMenu may need to reset the navigation scheme
        public void SetNavigationScheme(bool reset = false)
        {
            // Show mode sets its own navigation, don't overwrite
            if (MenuState == MenuState.Show)
            {
                return;
            }

            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            bool isSelectingPlaylist = MenuState == MenuState.PlaylistSelect;
            bool setListNotEmpty = ShowPlaylist.Count > 0;
            _sidebar.UpdatePlayButtonLabel(setListNotEmpty);
            NavigationScheme.Entry leftEntry = default;
            NavigationScheme.Entry rightEntry = default;

            if (MenuState == MenuState.Playlist)
            {
                leftEntry = new NavigationScheme.Entry(MenuAction.Left, "Menu.MusicLibrary.MoveInPlaylist", MovePlaylistEntryUp);
                rightEntry = new NavigationScheme.Entry(MenuAction.Right, "Menu.MusicLibrary.MoveInPlaylist", MovePlaylistEntryDown);
            }
            else
            {
                leftEntry = new NavigationScheme.Entry(MenuAction.Left, "Menu.MusicLibrary.SkipSection", GoToPreviousSection);
                rightEntry = new NavigationScheme.Entry(MenuAction.Right, "Menu.MusicLibrary.SkipSection", GoToNextSection);
            }

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx =>
                    {
                        if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange))
                        {
                            GoToPreviousSection();
                        }
                        else
                        {
                            SetWrapAroundState(!ctx.IsRepeat);
                            SelectedIndex--;
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx =>
                    {
                        if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange))
                        {
                            GoToNextSection();
                        }
                        else
                        {
                            SetWrapAroundState(!ctx.IsRepeat);
                            SelectedIndex++;
                        }
                    }),
                leftEntry,
                rightEntry,
                isSelectingPlaylist ?
                    new NavigationScheme.Entry(
                        MenuAction.Green,
                        "Menu.Common.Confirm",
                        () => CurrentSelection?.PrimaryButtonClick(),
                        hide: true
                    ) :
                    new NavigationScheme.Entry(
                        MenuAction.Green,
                        setListNotEmpty ?
                            "Menu.MusicLibrary.AddHoldStartSet" :
                            "Menu.MusicLibrary.PlayHoldAddToSet",
                        OnGreenTap,
                        GREEN_HOLD_SECONDS,
                        OnGreenHold,
                        hide: true
                    ),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back, hide: true),
                setListNotEmpty ?
                    new NavigationScheme.Entry(MenuAction.Yellow, "Menu.MusicLibrary.StartSet", StartSetlist) :
                    new NavigationScheme.Entry(MenuAction.Yellow, "Menu.MusicLibrary.PlayShow", EnterShowMode),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Filters", OpenFilters),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                    OnOrangeHit, OnOrangeRelease),
                new NavigationScheme.Entry(MenuAction.Select, "Next Sort Category", NextSort, hide: true),
            }, false));

        }

        protected override void OnSelectedIndexChanged()
        {
            const double PREVIEW_SCROLL_DELAY = .6f;
            base.OnSelectedIndexChanged();

            if (IsFiltersMenuOpen())
            {
                return;
            }

            _sidebar.UpdateSidebar();
            if (CurrentSelection is SongViewType song)
            {
                if (CurrentlyPlaying == null && song.SongEntry == _currentSong &&
                    (_previewCanceller == null || !_previewCanceller.IsCancellationRequested))
                {
                    return;
                }
                _currentSong = song.SongEntry;
            }
            else
            {
                _currentSong = null;
            }

            _previewCanceller?.Cancel();
            _previewCanceller = new CancellationTokenSource();
            _previewContext?.Stop();
            _previewContext = null;
            StartPreview(_previewDelay, _previewCanceller);

            _previewDelay = PREVIEW_SCROLL_DELAY;
        }


        protected override List<ViewType> CreateViewList()
        {
            // Shortcuts will be re-queried every time the list is refreshed
            _primaryHeaderIndex = 0;

            var viewList = MenuState switch
            {
                MenuState.Library        => CreateNormalViewList(),
                MenuState.PlaylistSelect => CreatePlaylistSelectViewList(),
                MenuState.Playlist       => CreatePlaylistViewList(),
                MenuState.Show           => CreateShowViewList(),
                _                        => throw new Exception("Unreachable.")
            };

            // Disable shortcuts if there are less than 2 sort headers in the viewlist
            HasSortHeaders = _sortedSongs is not null && _sortedSongs.Length > 1;

            return viewList;
        }

        private List<ViewType> CreateNormalViewList()
        {
            var list = new List<ViewType>();
            _totalStarCount = 0;

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0)
            {
                return list;
            }

            if (!_sortedSongs.Any(section => section.Songs.Length > 0))
            {
                list.Add(new SortHeaderViewType(Localize.Key("Menu.MusicLibrary.NoSongsMatchCriteria"), 0, null, Array.Empty<SongEntry>()));
                return list;
            }

            bool allowdupes = SettingsManager.Settings.AllowDuplicateSongs.Value;
            int songCount = 0;
            foreach (var section in _sortedSongs)
            {
                if (allowdupes)
                {
                    songCount += section.Songs.Length;
                    continue;
                }

                foreach (var song in section.Songs)
                {
                    if (!song.IsDuplicate)
                    {
                        ++songCount;
                    }
                }
            }

            if (!_searchField.IsSearching)
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.RandomSong"),
                    "MusicLibraryIcons[Random]",
                    SelectRandomSong,
                    RANDOM_SONG_ID));

                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Playlists"),
                    "MusicLibraryIcons[Playlists]",
                    EnterPlaylistSelectFromLibrary,
                    PLAYLIST_ID));

                _primaryHeaderIndex += 2;

                if (SettingsManager.Settings.LibrarySort < SortAttribute.Instrument &&
                    SettingsManager.Settings.ShowRecommendedSongs.Value)
                {
                    if (_recommendedSongs != null)
                    {
                        string key = Localize.Key("Menu.MusicLibrary.RecommendedSongs",
                            _recommendedSongs.Length == 1 ? "Singular" : "Plural");

                        list.Add(new CategoryViewType(key, _recommendedSongs.Length, _recommendedSongs,
                            () =>
                            {
                                bool selectTopOfList = CurrentSelection is SongViewType songView &&
                                    _recommendedSongs.Contains(songView.SongEntry);
                                RefreshAndReselect(selectTopOfList);
                            }
                        ));

                        foreach (var song in _recommendedSongs)
                        {
                            list.Add(new SongViewType(this, song));
                        }
                        _primaryHeaderIndex += _recommendedSongs.Length + 1;
                    }
                }
            }

            bool showSortHeaders = _sortedSongs.Length > 1 ||
                YARG.Menu.Filters.FiltersMenu.ActiveFilterPredicate != null;

            foreach (var section in _sortedSongs)
            {
                var displayName = section.Category;
                if (SettingsManager.Settings.LibrarySort == SortAttribute.Source)
                {
                    if (SongSources.TryGetSource(section.Category, out var parsedSource))
                    {
                        displayName = parsedSource.GetDisplayName();
                    }
                    else if (section.Category.Length > 0)
                    {
                        displayName = section.Category;
                    }
                    else
                    {
                        displayName = SongSources.Default.GetDisplayName();
                    }
                }

                SortHeaderViewType sortHeader = null;
                bool hideSearchResultsHeader = _searchField.IsSearching &&
                    string.Equals(section.Category, "Search Results", StringComparison.OrdinalIgnoreCase);
                if (showSortHeaders && !hideSearchResultsHeader)
                {
                    sortHeader = new SortHeaderViewType(displayName, section.Songs.Length, section.CategoryGroup, section.Songs);
                    list.Add(sortHeader);
                }

                int sectionTotalStars = 0;
                foreach (var song in section.Songs)
                {
                    if (allowdupes || !song.IsDuplicate)
                    {
                        var songView = new SongViewType(this, song);
                        list.Add(songView);

                        var starAmount = songView?.GetStarAmount();
                        sectionTotalStars += starAmount is null ? 0 : StarAmountHelper.GetStarCount(starAmount.Value);
                    }
                }
                _totalStarCount += sectionTotalStars;

                if (sortHeader != null)
                {
                    sortHeader.TotalStarsCount = sectionTotalStars;
                }

            }

            _totalSongCount = songCount;
            CalculateCategoryHeaderIndices(list);
            return list;
        }

        private void ExitLibrary()
        {
            ShowPlaylist.Clear();
            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            _previewContext = null;
            StemSettings.ApplySettings = true;
            MenuManager.Instance.PopMenu();
        }

        private void CalculateCategoryHeaderIndices(List<ViewType> list)
        {
            _sectionHeaderIndices.Clear();
            Shortcuts.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry is CategoryViewType)
                {
                    _sectionHeaderIndices.Add(i);
                }
                else if (entry is SortHeaderViewType header)
                {
                    _sectionHeaderIndices.Add(i);

                    string curShortcut = header.ShortcutName;

                    // Assume that any header with a ShortcutName of null is not meant to be included
                    // Add this shortcut if it does not match the one at end of the list
                    if (curShortcut != null &&
                        (Shortcuts.Count == 0 || curShortcut != Shortcuts[^1].Item1))
                    {
                        Shortcuts.Add((curShortcut, i));
                    }
                }
            }
        }

        private void SetRecommendedSongs()
        {
            if (!SettingsManager.Settings.ShowRecommendedSongs.Value)
            {
                _recommendedSongs = null;
                return;
            }

            if (SongContainer.Count > RecommendedSongs.RECOMMEND_SONGS_COUNT)
            {
                _recommendedSongs = RecommendedSongs.GetRecommendedSongs();
            }
            else
            {
                _recommendedSongs = null;
            }
        }

        private void Refresh()
        {
            SetRecommendedSongs();
            _searchField.Reset();
            UpdateSearch(true);
            SetNavigationScheme();
        }

        private void ClearPreview()
        {
            _currentSong = null;
            _previewCanceller?.Cancel();
            _previewContext?.Stop();
            _previewContext = null;
        }

        private void EnterPlaylistSelectFromLibrary()
        {
            MenuState = MenuState.PlaylistSelect;
            ClearPreview();

            Refresh();

            if (ViewList.Count > 0)
            {
                SelectedIndex = 0;
            }
            else
            {
                _sidebar.UpdateSidebar(true);
            }
        }

        private void UpdateSearch(bool force)
        {
            if (!force && _searchField.IsCurrentSearchInField)
            {
                return;
            }

            string previousSearch = _currentSearch;
            SongEntry previousSelectedSong = (CurrentSelection as SongViewType)?.SongEntry;
            int previousSelectedIndex = SelectedIndex;
            if (!PlaylistMode)
            {
                _sortedSongs = _searchField.Search(SettingsManager.Settings.LibrarySort);
                _searchField.gameObject.SetActive(true);
            }
            else
            {
                // Show playlist...

                var songs = new SongEntry[SelectedPlaylist.SongHashes.Count];
                int count = 0;
                foreach (var hash in SelectedPlaylist.SongHashes)
                {
                    // Get the first song with the specified hash
                    if (SongContainer.SongsByHash.TryGetValue(hash, out var song))
                    {
                        songs[count++] = song[0];
                    }
                }

                _sortedSongs = new SongCategory[]
                {
                    new(SelectedPlaylist.Name, songs[..count], null)
                };

                _searchField.gameObject.SetActive(false);
            }

            string currentSearch = _searchField.FullSearchQuery;
            bool searchChanged = !PlaylistMode &&
                !string.Equals(previousSearch, currentSearch, StringComparison.Ordinal);
            bool searchExpanded = !PlaylistMode && currentSearch.Length > previousSearch.Length;
            _currentSearch = currentSearch;

            if (_reloadState != MusicLibraryReloadState.Partial && !searchChanged &&
                MenuState != MenuState.PlaylistSelect)
            {
                int newPositionStartIndex = 0;
                if (_recommendedSongs != null)
                {
                    newPositionStartIndex = _primaryHeaderIndex;
                }

                if (_currentSong == null ||
                    !SetIndexTo(i => i is SongViewType view && view.SongEntry.SortBasedLocation == _currentSong.SortBasedLocation, newPositionStartIndex))
                {
                    // Note: it may look like this is expensive, but the whole loop should only last for 4-5 iterations
                    var list = ViewList;
                    int index = 0;
                    while (index < list.Count && list[index] is not CategoryViewType)
                    {
                        ++index;
                    }

                    while (index < list.Count && list[index] is not SongViewType)
                    {
                        ++index;
                    }

                    if (index == list.Count)
                    {
                        index = 0;
                    }
                    SelectedIndex = index;
                }
            }
            _searchField.UpdateSearchText();

            var predicate = YARG.Menu.Filters.FiltersMenu.ActiveFilterPredicate;
            bool inLibrary = !PlaylistMode && MenuState == MenuState.Library;
            bool shouldApplyFilters = inLibrary && predicate != null;
            bool shouldShowFilteredCounts = inLibrary && (_searchField.IsSearching || predicate != null);
            if (shouldApplyFilters)
            {
                _sortedSongs = ApplyFilterPredicate(_sortedSongs, predicate);
            }
            if (shouldShowFilteredCounts)
            {
                var baseList = SongContainer.GetSortedCategory(SettingsManager.Settings.LibrarySort);
                _totalSongCountUnfiltered = CountSongs(baseList);
            }
            else
            {
                _totalSongCountUnfiltered = 0;
            }
            RequestViewListUpdate();
            if (shouldApplyFilters)
            {
                EnsureValidSelectionAfterFilter();
            }

            // keep selection stable when the search text changes
            if (!PlaylistMode && searchChanged)
            {
                // jump to top when tightening search (adding characters)
                if (searchExpanded)
                {
                    _currentSong = null;
                    int targetIndex = 0;
                    for (int i = _primaryHeaderIndex; i < ViewList.Count; i++)
                    {
                        if (ViewList[i] is SongViewType)
                        {
                            targetIndex = i;
                            break;
                        }
                    }

                    if (SelectedIndex != targetIndex)
                    {
                        SelectedIndex = targetIndex;
                    }
                    else
                    {
                        OnSelectedIndexChanged();
                    }
                }
                // jump to most recent song when widening search (removing characters)
                else if (previousSelectedSong != null)
                {
                    if (!SetIndexTo(i => i is SongViewType view && view.SongEntry == previousSelectedSong, _primaryHeaderIndex))
                    {
                        SelectedIndex = Mathf.Clamp(previousSelectedIndex, 0, ViewList.Count - 1);
                    }
                }
            }

            UpdateSortInformationHeader();
        }

        private void EnsureValidSelectionAfterFilter()
        {
            if (ViewList.Count == 0)
            {
                _currentSong = null;
                return;
            }

            if (SelectedIndex < 0 || SelectedIndex >= ViewList.Count ||
                CurrentSelection is not SongViewType)
            {
                if (SetIndexTo(i => i is SongViewType, _primaryHeaderIndex))
                {
                    return;
                }

                SelectedIndex = Mathf.Clamp(SelectedIndex, 0, ViewList.Count - 1);
            }
        }

        private static int CountSongs(SongCategory[] categories)
        {
            int count = 0;
            foreach (var c in categories)
            {
                foreach (var s in c.Songs)
                {
                    if (!s.IsDuplicate || SettingsManager.Settings.AllowDuplicateSongs.Value)
                        count++;
                }
            }
            return count;
        }

        protected void Update()
        {
            foreach (var heldInput in _heldInputs)
                heldInput.Timer -= Time.unscaledDeltaTime;
        }

        private async void StartPreview(double delay, CancellationTokenSource canceller)
        {
            if (_currentSong == null)
            {
                return;
            }

            if (IsFiltersMenuOpen())
            {
                return;
            }

            const double FADE_DURATION = 1.25;
            float previewVolume = SettingsManager.Settings.PreviewVolume.Value;
            if (previewVolume == 0)
            {
                return;
            }

            var context = await PreviewContext.Create(_currentSong, previewVolume, GlobalVariables.State.SongSpeed,
                delay, FADE_DURATION, canceller);
            if (context != null)
            {
                _previewContext = context;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetSidebarDifficultiesVisible(false);

            if (Navigator.Instance == null) return;

            // Save state
            _savedIndex = SelectedIndex;
            _savedPlaylist = SelectedPlaylist;
            bool preferHeaderOverFallback = _preferHeaderOnNextSnapshot;
            _preferHeaderOnNextSnapshot = false;
            if (MenuState == MenuState.Library && !PlaylistMode)
            {
                bool preserveIndexOnDynamicSort = SettingsManager.Settings.LibrarySort == SortAttribute.Playcount ||
                    SettingsManager.Settings.LibrarySort == SortAttribute.Stars;
                _savedSelectionSnapshot = CaptureSelectionSnapshot(preserveIndexOnDynamicSort, preferHeaderOverFallback);
                _hasSavedSelectionSnapshot = true;
            }
            else
            {
                _hasSavedSelectionSnapshot = false;
            }

            Navigator.Instance.PopScheme();

            _previewCanceller?.Cancel();
            _previewContext?.Stop();
            _searchField.OnSearchQueryUpdated -= UpdateSearch;

            PlayerContainer.PlayerAdded -= OnPlayerAdded;
            PlayerContainer.PlayerRemoved -= OnPlayerRemoved;
        }

        private void OnDestroy()
        {
            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            _reloadState = MusicLibraryReloadState.Partial;
            StemSettings.ApplySettings = true;
        }

        public void Back()
        {
            if (_searchField.IsSearching)
            {
                _searchField.ClearFilterQueries();
                return;
            }

            switch(MenuState)
            {
                case MenuState.Playlist:
                    ExitPlaylistView();
                    break;
                case MenuState.PlaylistSelect:
                    ExitPlaylistSelect();
                    break;
                case MenuState.Show:
                    LeaveShowMode();
                    break;
                case MenuState.Library:
                    ExitLibrary();
                    break;
            }
        }

        public void NextSort()
        {
            SortAttribute nextSort;
            if (SettingsManager.Settings.LibrarySort >= SortAttribute.Playable)
            {
                nextSort = SortAttribute.Name;
            }
            else
            {
                 nextSort = (SortAttribute) ((int) SettingsManager.Settings.LibrarySort + 1);
            }

            ChangeSort(nextSort);
        }

        private bool IsButtonHeldByPlayer(YargPlayer player, MenuAction button)
        {
            return _heldInputs.Any(i => i.Context.Player == player && i.Context.Action == button);
        }

        private const float GREEN_HOLD_SECONDS = 1f;

        private void OnGreenTap(NavigationContext _)
        {
            ExecuteGreenTapAction();
        }

        public void ExecuteGreenTapAction()
        {
            bool setListNotEmpty = ShowPlaylist.Count > 0;

            if (setListNotEmpty)
            {
                // same as Yellow: Add to Setlist
                AddToPlaylist();
            }
            else
            {
                // same as old Green confirm: Play song
                CurrentSelection?.PrimaryButtonClick();
            }
        }

        private void OnGreenHold(NavigationContext _)
        {
            ExecuteGreenHoldAction();
        }

        public void ExecuteGreenHoldAction()
        {
            bool setListNotEmpty = ShowPlaylist.Count > 0;

            if (setListNotEmpty)
            {
                // same as Blue: Start Setlist
                StartSetlist();
            }
            else
            {
                // same as Yellow: Add to Setlist
                AddToPlaylist();
            }
        }

        public string GetGreenHoldActionLabel()
        {
            bool setListNotEmpty = ShowPlaylist.Count > 0;
            return Localize.Key(setListNotEmpty ? "Menu.MusicLibrary.StartSet" : "Menu.MusicLibrary.AddToSet");
        }

        private void OnOrangeHit(NavigationContext ctx)
        {
            _heldInputs.Add(new HoldContext(ctx));
        }

        private void OnOrangeRelease(NavigationContext ctx)
        {
            var holdContext = _heldInputs.FirstOrDefault(i => i.Context.IsSameAs(ctx));

            if (ctx.Action == MenuAction.Orange && (holdContext?.Timer > 0 || ctx.Player is null))
                _popupMenu.gameObject.SetActive(true);

            _heldInputs.RemoveAll(i => i.Context.IsSameAs(ctx));
        }

        private void GoToNextSection()
        {
            var i = _sectionHeaderIndices.BinarySearch(SelectedIndex);
            i = i < 0 ? ~i : i + 1;
            if (i >= _sectionHeaderIndices.Count)
                return;

            SelectedIndex = _sectionHeaderIndices[i];
        }

        private void GoToPreviousSection()
        {
            var i = _sectionHeaderIndices.BinarySearch(SelectedIndex);
            i = i < 0 ? ~i - 1 : i - 1;
            if (i < 0)
                return;

            SelectedIndex = _sectionHeaderIndices[i];
        }

        public void SelectRandomSong()
        {
            if (!ViewList.Any(i => i is SongViewType)) return;

            do
            {
                SelectedIndex = Random.Range(0, ViewList.Count);
            } while (CurrentSelection is not SongViewType);
        }

        public void RefreshAndReselect(bool selectTopOfList = false)
        {
            var snapshot = CaptureSelectionSnapshot();
            Refresh();

            if (selectTopOfList)
            {
                if (SetIndexToFirstRecommendedSong()) return;

                if (SetIndexTo(i => i is SongViewType)) return;

                SelectedIndex = 0;
                return;
            }

            RestoreSelectionSnapshot(snapshot);
        }

        private (string headerText, string headerShortcut, string categoryText) GetHeaderSnapshotAboveIndex(int startIndex)
        {
            var list = ViewList;
            for (int i = Math.Min(startIndex, list.Count - 1); i >= 0; i--)
            {
                switch (list[i])
                {
                    case SortHeaderViewType sortHeader:
                        return (sortHeader.HeaderText, sortHeader.ShortcutName, null);
                    case CategoryViewType category:
                        return (null, null, category.GetPrimaryText(false));
                }
            }

            return (null, null, null);
        }

        private SongEntry GetFirstSongAfterIndex(int startIndex)
        {
            var list = ViewList;
            for (int i = startIndex + 1; i < list.Count; i++)
            {
                if (list[i] is SongViewType songView)
                    return songView.SongEntry;
            }

            return null;
        }

        private readonly struct SelectionSnapshot
        {
            public readonly int SelectedIndex;
            public readonly SongEntry PreviousSong;

            public readonly string HeaderText;
            public readonly string HeaderShortcut;
            public readonly string CategoryText;

            public readonly SongEntry FallbackSong;
            public readonly int? ButtonId;

            public readonly bool WasRecommendedHeader;
            public readonly bool WasRecommendedSong;

            public readonly bool PreserveIndexOnDynamicSort;
            public readonly bool PreferHeaderOverFallback;

            public readonly HashWrapper? PreviousSongHash;
            public readonly string PreviousSongLocation;

            public readonly HashWrapper? FallbackSongHash;
            public readonly string FallbackSongLocation;

            public SelectionSnapshot(
                int selectedIndex,
                SongEntry previousSong,

                string headerText,
                string headerShortcut,
                string categoryText,

                SongEntry fallbackSong,
                int? buttonId,

                bool wasRecommendedHeader,
                bool wasRecommendedSong,

                bool preserveIndexOnDynamicSort,
                bool preferHeaderOverFallback,

                HashWrapper? previousSongHash,
                string previousSongLocation,

                HashWrapper? fallbackSongHash,
                string fallbackSongLocation)
            {
                SelectedIndex = selectedIndex;
                PreviousSong = previousSong;
                HeaderText = headerText;
                HeaderShortcut = headerShortcut;
                CategoryText = categoryText;
                FallbackSong = fallbackSong;
                ButtonId = buttonId;
                WasRecommendedHeader = wasRecommendedHeader;
                WasRecommendedSong = wasRecommendedSong;
                PreserveIndexOnDynamicSort = preserveIndexOnDynamicSort;
                PreferHeaderOverFallback = preferHeaderOverFallback;

                PreviousSongHash = previousSongHash;
                PreviousSongLocation = previousSongLocation;

                FallbackSongHash = fallbackSongHash;
                FallbackSongLocation = fallbackSongLocation;
            }
        }

        private SelectionSnapshot CaptureSelectionSnapshot(bool preserveIndexOnDynamicSort = false, bool preferHeaderOverFallback = false)
        {
            // Resolution rules
            if (!preferHeaderOverFallback && _preferHeaderOnNextSnapshot)
            {
                preferHeaderOverFallback = true;
                _preferHeaderOnNextSnapshot = false;
            }

            // Selection
            int selectedIndex = SelectedIndex;
            SongEntry previousSong = null;
            if (CurrentSelection is SongViewType songView)
            {
                // When FiltersMenu is open, _currentSong stops updating.
                previousSong = songView.SongEntry ?? _currentSong;
            }
            else
            {
                previousSong = _currentSong;
            }

            // Header context
            string headerText = null;
            string headerShortcut = null;
            string categoryText = null;

            // Trigger source
            SongEntry fallbackSong = null;
            int? buttonId = null;

            // Recommendation state
            bool wasRecommendedHeader = false;
            bool wasRecommendedSong = false;
            bool isInRecommendedSection = false;

            // Stable identifiers
            HashWrapper? previousSongHash = previousSong?.Hash;
            string previousSongLocation = previousSong?.ActualLocation;
            HashWrapper? fallbackSongHash = null;
            string fallbackSongLocation = null;

            if (MenuState == MenuState.Library && !PlaylistMode)
            {
                if (CurrentSelection is ButtonViewType button)
                {
                    buttonId = button.ID;
                }
                else if (previousSong == null && CurrentSelection is not SongViewType)
                {
                    fallbackSong = GetFirstSongAfterIndex(selectedIndex);
                }

                var headerSnapshot = GetHeaderSnapshotAboveIndex(selectedIndex);
                headerText = headerSnapshot.headerText;
                headerShortcut = headerSnapshot.headerShortcut;
                categoryText = headerSnapshot.categoryText;
                wasRecommendedHeader = CurrentSelection is CategoryViewType && _recommendedSongs != null;
                if (CurrentSelection is SongViewType && _recommendedSongs != null && _recommendedSongs.Length > 0)
                {
                    int recommendedHeaderIndex = -1;
                    for (int i = 0; i < ViewList.Count; i++)
                    {
                        if (ViewList[i] is CategoryViewType)
                        {
                            recommendedHeaderIndex = i;
                            break;
                        }
                    }

                    if (recommendedHeaderIndex != -1)
                    {
                        int startIndex = recommendedHeaderIndex + 1;
                        int endIndex = recommendedHeaderIndex + _recommendedSongs.Length;
                        isInRecommendedSection = selectedIndex >= startIndex && selectedIndex <= endIndex;
                    }

                    wasRecommendedSong = isInRecommendedSection && _recommendedSongs.Contains(_currentSong);
                }
            }

            if (fallbackSong != null)
            {
                fallbackSongHash = fallbackSong.Hash;
                fallbackSongLocation = fallbackSong.ActualLocation;
            }

            return new SelectionSnapshot(
                selectedIndex,
                previousSong,

                headerText,
                headerShortcut,
                categoryText,

                fallbackSong,
                buttonId,

                wasRecommendedHeader,
                wasRecommendedSong,

                preserveIndexOnDynamicSort,
                preferHeaderOverFallback,

                previousSongHash,
                previousSongLocation,

                fallbackSongHash,
                fallbackSongLocation);
        }

        private void RestoreSelectionSnapshot(SelectionSnapshot snapshot)
        {
            if (snapshot.PreserveIndexOnDynamicSort &&
                (SettingsManager.Settings.LibrarySort == SortAttribute.Playcount ||
                    SettingsManager.Settings.LibrarySort == SortAttribute.Stars))
            {
                if (ViewList.Count == 0) return;

                SelectedIndex = Mathf.Clamp(snapshot.SelectedIndex, 0, ViewList.Count - 1);
                return;
            }

            if (snapshot.WasRecommendedSong && _recommendedSongs == null &&
                SetIndexTo(i => i is SortHeaderViewType, _primaryHeaderIndex))
            {
                return;
            }

            if (snapshot.WasRecommendedSong && _recommendedSongs != null && snapshot.PreviousSong != null)
            {
                if (!_recommendedSongs.Contains(snapshot.PreviousSong) &&
                    SetIndexTo(i => i is CategoryViewType))
                {
                    return;
                }
            }

            if (snapshot.WasRecommendedSong && _recommendedSongs != null &&
                SetIndexTo(i => i is SongViewType view &&
                    snapshot.PreviousSongHash.HasValue &&
                    view.SongEntry.Hash.Equals(snapshot.PreviousSongHash.Value)))
            {
                return;
            }

            if (snapshot.WasRecommendedSong && _recommendedSongs != null &&
                SetIndexTo(i => i is SongViewType view &&
                    snapshot.PreviousSongLocation != null &&
                    view.SongEntry.ActualLocation == snapshot.PreviousSongLocation))
            {
                return;
            }

            if (snapshot.WasRecommendedSong && _recommendedSongs != null &&
                snapshot.PreviousSong != null &&
                SetIndexTo(i => i is SongViewType view &&
                    view.SongEntry.SortBasedLocation == snapshot.PreviousSong.SortBasedLocation))
            {
                return;
            }

            if (snapshot.PreviousSongHash.HasValue &&
                SetIndexTo(i => i is SongViewType view &&
                    view.SongEntry.Hash.Equals(snapshot.PreviousSongHash.Value),
                    _primaryHeaderIndex))
            {
                return;
            }

            if (snapshot.PreviousSongLocation != null &&
                SetIndexTo(i => i is SongViewType view &&
                    view.SongEntry.ActualLocation == snapshot.PreviousSongLocation,
                    _primaryHeaderIndex))
            {
                return;
            }

            if (snapshot.PreviousSong != null &&
                SetIndexTo(i => i is SongViewType view &&
                    view.SongEntry.SortBasedLocation == snapshot.PreviousSong.SortBasedLocation,
                    _primaryHeaderIndex))
            {
                return;
            }

            if (snapshot.ButtonId.HasValue &&
                SetIndexTo(i => i is ButtonViewType button && button.ID == snapshot.ButtonId.Value))
            {
                return;
            }

            if (snapshot.WasRecommendedHeader)
            {
                if (_recommendedSongs != null &&
                    SetIndexTo(i => i is CategoryViewType))
                {
                    return;
                }

                if (_recommendedSongs == null &&
                    SetIndexTo(i => i is SortHeaderViewType, _primaryHeaderIndex))
                {
                    return;
                }
            }

            bool headerFirst = snapshot.PreferHeaderOverFallback ||
                (snapshot.PreviousSong == null && snapshot.HeaderText != null);
            if (headerFirst)
            {
                if (snapshot.HeaderText != null &&
                    SetIndexTo(i => i is SortHeaderViewType header &&
                        header.HeaderText == snapshot.HeaderText &&
                        header.ShortcutName == snapshot.HeaderShortcut))
                {
                    return;
                }

                if (snapshot.CategoryText != null &&
                    SetIndexTo(i => i is CategoryViewType category &&
                        category.GetPrimaryText(false) == snapshot.CategoryText))
                {
                    return;
                }

                if (snapshot.FallbackSongHash.HasValue &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.Hash.Equals(snapshot.FallbackSongHash.Value),
                        _primaryHeaderIndex))
                {
                    return;
                }

                if (snapshot.FallbackSongLocation != null &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.ActualLocation == snapshot.FallbackSongLocation,
                        _primaryHeaderIndex))
                {
                    return;
                }

                if (snapshot.FallbackSong != null &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.SortBasedLocation == snapshot.FallbackSong.SortBasedLocation,
                        _primaryHeaderIndex))
                {
                    return;
                }
            }
            else
            {
                if (snapshot.FallbackSongHash.HasValue &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.Hash.Equals(snapshot.FallbackSongHash.Value),
                        _primaryHeaderIndex))
                {
                    return;
                }

                if (snapshot.FallbackSongLocation != null &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.ActualLocation == snapshot.FallbackSongLocation,
                        _primaryHeaderIndex))
                {
                    return;
                }

                if (snapshot.FallbackSong != null &&
                    SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.SortBasedLocation == snapshot.FallbackSong.SortBasedLocation,
                        _primaryHeaderIndex))
                {
                    return;
                }

                if (snapshot.HeaderText != null &&
                    SetIndexTo(i => i is SortHeaderViewType header &&
                        header.HeaderText == snapshot.HeaderText &&
                        header.ShortcutName == snapshot.HeaderShortcut))
                {
                    return;
                }

                if (snapshot.CategoryText != null &&
                    SetIndexTo(i => i is CategoryViewType category &&
                        category.GetPrimaryText(false) == snapshot.CategoryText))
                {
                    return;
                }
            }

            if (SetIndexTo(i => i is SongViewType))
            {
                return;
            }

            SelectedIndex = snapshot.SelectedIndex;
        }

        private bool SetIndexToFirstRecommendedSong()
        {
            if (_recommendedSongs == null || _recommendedSongs.Length == 0)
                return false;

            var recommendedSet = new HashSet<SongEntry>(_recommendedSongs);
            return SetIndexTo(i => i is SongViewType view && recommendedSet.Contains(view.SongEntry));
        }

        public void RefreshSidebar()
        {
            _sidebar.RefreshFavoriteState();
        }

        public void SetSidebarDifficultiesVisible(bool visible)
        {
            _sidebar?.SetDifficultiesVisible(visible);
        }

        public void RequestPreferHeaderOnNextSnapshot()
        {
            _preferHeaderOnNextSnapshot = true;
        }

        public void ChangeSort(SortAttribute sort)
        {
            var snapshot = CaptureSelectionSnapshot();

            // Keep the previous sort attribute, too, so it can be used to
            // sort the list of unplayed songs and possibly for other things
            if (sort != SortAttribute.Playcount && sort != SortAttribute.Stars)
            {
                SettingsManager.Settings.PreviousLibrarySort = sort;
            }
            SettingsManager.Settings.LibrarySort = sort;
            UpdateSearch(true);
            RestoreSelectionSnapshot(snapshot);
        }

        private void UpdateSortInformationHeader()
        {
            if (MenuState == MenuState.Library)
            {
                if (_searchField.IsSearching)
                {
                    _sortInfoHeaderPrimaryText.text = TextColorer.StyleString(
                        Localize.Key("Menu.MusicLibrary.SearchResults"),
                        MenuData.Colors.HeaderSecondary,
                        700);
                }
                else if (SettingsManager.Settings.LibrarySort < SortAttribute.Instrument)
                {
                    var sortingBy = TextColorer.StyleString("SORTED BY ",
                        MenuData.Colors.HeaderTertiary,
                        600);

                    var sortKey = TextColorer.StyleString(SettingsManager.Settings.LibrarySort.ToLocalizedName(),
                        MenuData.Colors.HeaderSecondary,
                        700);

                    _sortInfoHeaderPrimaryText.text = ZString.Concat(sortingBy, sortKey);
                }
                else
                {
                    var playableSongs = TextColorer.StyleString("PLAYABLE ON ",
                        MenuData.Colors.HeaderTertiary,
                        600);

                    var sortKey = TextColorer.StyleString(SettingsManager.Settings.LibrarySort.ToLocalizedName(),
                        MenuData.Colors.HeaderSecondary,
                        700);

                    _sortInfoHeaderPrimaryText.text = ZString.Concat(playableSongs, sortKey);
                }

                string countText;
                if (_totalSongCountUnfiltered > 0 && _totalSongCount != _totalSongCountUnfiltered)
                {
                    var filtered = TextColorer.StyleString(ZString.Format("{0:N0}", _totalSongCount),
                        MenuData.Colors.HeaderSecondary, 500);
                    var total = TextColorer.StyleString(ZString.Format("{0:N0}", _totalSongCountUnfiltered),
                        MenuData.Colors.HeaderTertiary, 600);

                    countText = ZString.Concat(filtered, " / ", total);
                }
                else
                {
                    countText = TextColorer.StyleString(ZString.Format("{0:N0}", _totalSongCount),
                        MenuData.Colors.HeaderSecondary, 500);
                }

                var songs = TextColorer.StyleString(
                    _totalSongCount == 1 ? "SONG" : "SONGS",
                    MenuData.Colors.HeaderTertiary, 600);

                _sortInfoHeaderSongCountText.text = ZString.Concat(countText, " ", songs);

                var obtainedStars = TextColorer.StyleString(
                    ZString.Format("{0}", _totalStarCount),
                    MenuData.Colors.HeaderSecondary,
                    700);

                var totalStars = TextColorer.StyleString(
                    ZString.Format(" / {0}", _totalSongCount * 5),
                    MenuData.Colors.HeaderTertiary,
                    600);

                _sortInfoHeaderStarCountText.text = ZString.Concat(obtainedStars, totalStars);
                _sortInfoHeaderStarIcon.color = _sortInfoHeaderStarIcon.color.WithAlpha(1);
            }
            else if (MenuState == MenuState.PlaylistSelect)
            {
                _numPlaylists = GetPlaylistCountForHeader();

                _sortInfoHeaderPrimaryText.text = ZString.Concat(
                    TextColorer.StyleString("SHOWING ", MenuData.Colors.HeaderTertiary, 600),
                    TextColorer.StyleString("ALL PLAYLISTS", MenuData.Colors.HeaderSecondary, 700));

                var count = TextColorer.StyleString(
                    ZString.Format("{0:N0}", _numPlaylists),
                    MenuData.Colors.HeaderSecondary,
                    500);

                var playlists = TextColorer.StyleString(
                    _numPlaylists == 1 ? "PLAYLIST" : "PLAYLISTS",
                    MenuData.Colors.HeaderTertiary,
                    600);

                _sortInfoHeaderSongCountText.text = ZString.Concat(count, " ", playlists);
                _sortInfoHeaderStarCountText.text = "";
                _sortInfoHeaderStarIcon.color = _sortInfoHeaderStarIcon.color.WithAlpha(0);
            }
            else if (MenuState == MenuState.Playlist)
            {
                _sortInfoHeaderPrimaryText.text = ZString.Concat(
                    TextColorer.StyleString("PLAYLIST ", MenuData.Colors.HeaderTertiary, 600),
                    TextColorer.StyleString(SelectedPlaylist.Name, MenuData.Colors.HeaderSecondary, 700));

                var countText = TextColorer.StyleString(ZString.Format("{0:N0}", _totalSongCount),
                    MenuData.Colors.HeaderSecondary, 500);
                var songs = TextColorer.StyleString(
                    _totalSongCount == 1 ? "SONG" : "SONGS",
                    MenuData.Colors.HeaderTertiary, 600);
                _sortInfoHeaderSongCountText.text = ZString.Concat(countText, " ", songs);

                var obtainedStars = TextColorer.StyleString(
                    ZString.Format("{0}", _totalStarCount),
                    MenuData.Colors.HeaderSecondary,
                    700);
                var totalStars = TextColorer.StyleString(
                    ZString.Format(" / {0}", _totalSongCount * 5),
                    MenuData.Colors.HeaderTertiary,
                    600);
                _sortInfoHeaderStarCountText.text = ZString.Concat(obtainedStars, totalStars);
                _sortInfoHeaderStarIcon.color = _sortInfoHeaderStarIcon.color.WithAlpha(1);
            }
        }

        private int GetPlaylistCountForHeader()
        {
            int count = 1; // Favorites
            if (ShowPlaylist.Count > 0)
                count++;

            count += PlaylistContainer.Playlists.Count;
            return count;
        }

        public void SetSearchInput(SortAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
            UpdateSearch(true);
        }

        private void OpenFilters()
        {
            // Stop any library preview audio so the Filters menu doesn't inherit it
            _previewCanceller?.Cancel();
            _previewContext?.Stop();
            _previewContext = null;

            var menu = YARG.Menu.Filters.FiltersMenu.Instance;
            if (menu == null)
                return;

            menu.gameObject.SetActive(true);
            _sidebar.SetDifficultiesVisible(false);
        }

        private static bool IsFiltersMenuOpen()
        {
            var menu = YARG.Menu.Filters.FiltersMenu.Instance;
            return menu != null && menu.gameObject.activeInHierarchy;
        }

        private static SongCategory[] ApplyFilterPredicate(SongCategory[] categories, Func<SongEntry, bool> predicate)
        {
            var result = new SongCategory[categories.Length];
            int count = 0;

            foreach (var category in categories)
            {
                var songs = category.Songs.Where(predicate).ToArray();
                if (songs.Length > 0)
                {
                    result[count++] = new SongCategory(category.Category, songs, category.CategoryGroup);
                }
            }

            return result[..count];
        }

        public async void RefreshSongs()
        {
            // Stop any library preview audio so the loading screen doesn't inherit it
            _previewCanceller?.Cancel();
            _previewContext?.Stop();
            _previewContext = null;

            SetSidebarDifficultiesVisible(false);
            using var context = new LoadingContext();
            try
            {
                await SongContainer.RunRefresh(false, context);
                RefreshAndReselect();
            }
            finally
            {
                // Ensure difficulty rings are restored even if the scan fails or is canceled
                SetSidebarDifficultiesVisible(true);
            }
        }

        private void OnPlayerAdded(YargPlayer player)
        {
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);
        }

        private void OnPlayerRemoved(YargPlayer player)
        {
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);
        }

        public static void ResetMainLibraryIndex()
        {
            _mainLibraryIndex = -1;
        }
    }
}
