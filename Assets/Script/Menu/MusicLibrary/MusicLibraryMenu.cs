using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
using YARG.Menu.Persistent;
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
        Playlist
    }

    public class MusicLibraryMenu : ListMenu<ViewType, SongView>
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
        public Playlist        ShowPlaylist { get; set; } = new(true);

        private int _primaryHeaderIndex;

        protected override void Awake()
        {
            base.Awake();

            // Initialize sidebar
            _sidebar.Initialize(this, _searchField);

            // Fill in sort information
            UpdateSortInformationHeader();
        }

        private void OnEnable()
        {
            // Set navigation scheme
            SetNavigationScheme();

            // Restore search
            _searchField.Restore();
            _searchField.OnSearchQueryUpdated += UpdateSearch;

            if (CurrentlyPlaying != null)
            {
                _currentSong = CurrentlyPlaying;
            }

            ShouldDisplaySoloHighScores = PlayerContainer.Players.Count(e => !e.Profile.IsBot) == 1;

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
                    MenuState = MenuState.Playlist;
                }

                UpdateSearch(true);
                SelectedIndex = _savedIndex;
            }
            else if (_currentSong != null)
            {
                UpdateSearch(true);
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
                SettingsManager.Settings.LibrarySort == SortAttribute.Playcount)
            {
                // Name makes a good fallback?
                ChangeSort(SortAttribute.Name);
            }

            // Fill in sort information
            UpdateSortInformationHeader();
        }

        // Public because PopupMenu may need to reset the navigation scheme
        public void SetNavigationScheme(bool reset = false)
        {
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            bool isSelectingPlaylist = MenuState == MenuState.PlaylistSelect;

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
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => CurrentSelection?.PrimaryButtonClick(), hide: !isSelectingPlaylist),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.MusicLibrary.AddToSet",
                    AddToSetlist),
                isSelectingPlaylist ?
                    new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.StartSet", StartSetlist) :
                    new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Search", _searchField.Focus),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                    OnButtonHit, OnButtonRelease),
                new NavigationScheme.Entry(MenuAction.Select, "Next Sort Category", NextSort, hide: true),
            }, false));

        }

        protected override void OnSelectedIndexChanged()
        {
            const double PREVIEW_SCROLL_DELAY = .6f;
            base.OnSelectedIndexChanged();

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
                _                        => throw new Exception("Unreachable.")
            };

            // Disable shortcuts if there are less than 2 sort headers in the viewlist
            HasSortHeaders = _sortedSongs is not null && _sortedSongs.Length > 1;

            return viewList;
        }

        private List<ViewType> CreatePlaylistSelectViewList()
        {
            _numPlaylists = 0;
            SongCategory[] emptyCategory = Array.Empty<SongCategory>();
            int id = BACK_ID + 1;
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"),
                    "MusicLibraryIcons[Back]", () =>
                    {
                        SelectedPlaylist = null;
                        MenuState = MenuState.Library;
                        Refresh();
                    }, BACK_ID)
            };

            list.Add(new ButtonViewType("YARG", "MusicLibraryIcons[Playlists]", () => { }));;
            // Favorites is always on top
            list.Add(new PlaylistViewType(
                Localize.Key("Menu.MusicLibrary.Favorites"),
                PlaylistContainer.FavoritesPlaylist,
                () =>
                {
                    SelectedPlaylist = PlaylistContainer.FavoritesPlaylist;
                    MenuState = MenuState.Playlist;
                    Refresh();
                }, PLAYLIST_ID));
            _numPlaylists++;


            list.Add(new ButtonViewType(Localize.Key("Menu.MusicLibrary.YourPlaylists"),
                "MusicLibraryIcons[Playlists]", () => { }));

            // Add the setlist "playlist" if there are any songs currently in it
            if (ShowPlaylist.Count > 0)
            {
                list.Add(new PlaylistViewType(Localize.Key("Menu.MusicLibrary.CurrentSetlist"), ShowPlaylist,
                    () =>
                    {
                        SelectedPlaylist = ShowPlaylist;
                        MenuState = MenuState.Playlist;
                        Refresh();
                    }, id));
                id++;
                _numPlaylists++;
            }

            // Add any other user defined playlists
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                list.Add(new PlaylistViewType(playlist.Name, playlist, () =>
                {
                    SelectedPlaylist = playlist;
                    MenuState = MenuState.Playlist;
                    Refresh();
                }, id));
                id++;
                _numPlaylists++;
            }

            return list;
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
                list.Add(new SortHeaderViewType(Localize.Key("Menu.MusicLibrary.NoSongsMatchCriteria"), 0, null));
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

            if (_searchField.IsSearching)
            {
                list.Add(new CategoryViewType(Localize.Key("Menu.MusicLibrary.SearchResults"), songCount, _sortedSongs));
            }
            else
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.RandomSong"),
                    "MusicLibraryIcons[Random]",
                    SelectRandomSong,
                    RANDOM_SONG_ID));

                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Playlists"),
                    "MusicLibraryIcons[Playlists]",
                    () =>
                    {
                        MenuState = MenuState.PlaylistSelect;
                        Refresh();
                    },
                    PLAYLIST_ID));

                _primaryHeaderIndex += 2;

                if (SettingsManager.Settings.LibrarySort <= SortAttribute.Playcount)
                {
                    if (_recommendedSongs != null)
                    {
                        string key = Localize.Key("Menu.MusicLibrary.RecommendedSongs",
                            _recommendedSongs.Length == 1 ? "Singular" : "Plural");

                        list.Add(new CategoryViewType(key, _recommendedSongs.Length, _recommendedSongs,
                            () =>
                            {
                                SetRecommendedSongs();
                                RefreshAndReselect();
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
                if (_sortedSongs.Length > 1)
                {
                    sortHeader = new SortHeaderViewType(displayName, section.Songs.Length, section.CategoryGroup);
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

        private List<ViewType> CreatePlaylistViewList()
        {
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"),
                    "MusicLibraryIcons[Back]", ExitPlaylistView, BACK_ID)
            };

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0 ||
                !_sortedSongs.Any(section => section.Songs.Length > 0))
            {
                return list;
            }

            bool allowdupes = SettingsManager.Settings.AllowDuplicateSongs.Value;
            foreach (var section in _sortedSongs)
            {
                list.Add(new SortHeaderViewType(
                    section.Category.ToUpperInvariant(),
                    section.Songs.Length,
                    section.CategoryGroup));

                foreach (var song in section.Songs)
                {
                    if (allowdupes || !song.IsDuplicate)
                    {
                        list.Add(new SongViewType(this, song));
                    }
                }
            }

            CalculateCategoryHeaderIndices(list);
            return list;
        }

        private void ExitPlaylistView()
        {
            SelectedPlaylist = null;
            MenuState = MenuState.PlaylistSelect;
            Refresh();

            // Select playlist button
            // TODO: Fix this to select the playlist we entered from, not favorites
            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
        }

        private void ExitPlaylistSelect()
        {
            MenuState = MenuState.Library;
            Refresh();

            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
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
            UpdateSortInformationHeader();
            SetNavigationScheme();
        }

        private void UpdateSearch(bool force)
        {
            if (!force && _searchField.IsCurrentSearchInField)
            {
                return;
            }

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

            RequestViewListUpdate();

            if (_reloadState != MusicLibraryReloadState.Partial)
            {
                int newPositionStartIndex = 0;
                if (_recommendedSongs != null)
                {
                    newPositionStartIndex = _primaryHeaderIndex;
                }

                if (_searchField.IsUpdatedSearchLonger || _currentSong == null ||
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
        }

        protected override void Update()
        {
            foreach (var heldInput in _heldInputs)
                heldInput.Timer -= Time.unscaledDeltaTime;

            base.Update();
        }

        private async void StartPreview(double delay, CancellationTokenSource canceller)
        {
            if (_currentSong == null)
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

        private void OnDisable()
        {
            if (Navigator.Instance == null) return;

            // Save state
            _savedIndex = SelectedIndex;
            _savedPlaylist = SelectedPlaylist;

            Navigator.Instance.PopScheme();

            _previewCanceller?.Cancel();
            _previewContext?.Stop();
            _searchField.OnSearchQueryUpdated -= UpdateSearch;
        }

        private void OnDestroy()
        {
            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            _reloadState = MusicLibraryReloadState.Partial;
            StemSettings.ApplySettings = true;
        }

        private void Back()
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
                case MenuState.Library:
                    ExitLibrary();
                    break;
            }
        }

        public void NextSort()
        {
            SortAttribute nextSort;
            if (SettingsManager.Settings.LibrarySort >= SortAttribute.Playcount)
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

        private void OnButtonHit(NavigationContext ctx)
        {
            _heldInputs.Add(new HoldContext(ctx));
        }

        private void OnButtonRelease(NavigationContext ctx)
        {
            var holdContext = _heldInputs.FirstOrDefault(i => i.Context.IsSameAs(ctx));

            if (ctx.Action == MenuAction.Orange && (holdContext?.Timer > 0 || ctx.Player is null))
                _popupMenu.gameObject.SetActive(true);

            _heldInputs.RemoveAll(i => i.Context.IsSameAs(ctx));
        }

        private void AddToSetlist(NavigationContext ctx)
        {
            if (CurrentSelection is PlaylistViewType playlist)
            {
                if (playlist.Playlist.SongHashes.Count == 0)
                {
                    ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.EmptyPlaylist"));
                    return;
                }

                if (playlist.Playlist.Ephemeral)
                {
                    // No, we won't add the setlist to itself, thanks
                    ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.CannotAddToSelf"));
                    return;
                }

                var i = 0;

                foreach (var song in playlist.Playlist.ToList())
                {
                    ShowPlaylist.AddSong(song);
                    i++;
                }

                if (i > 0)
                {
                    ToastManager.ToastSuccess(Localize.KeyFormat("Menu.MusicLibrary.PlaylistAddedToSet", i));
                }
                else
                {
                    ToastManager.ToastWarning(Localize.Key("Menu.MusicLibrary.NoSongsInPlaylist"));
                }

                if (i > 0 && ShowPlaylist.Count == i)
                {
                    // We need to rebuild the navigation scheme the first time we add song(s)
                    SetNavigationScheme(true);
                }

                // If we are in the playlist view, we need to refresh the view
                if (MenuState == MenuState.PlaylistSelect)
                {
                    RefreshAndReselect();
                }

                return;
            }

            if (CurrentSelection is SongViewType selection)
            {
                ShowPlaylist.AddSong(selection.SongEntry);
                if (ShowPlaylist.Count == 1)
                {
                    // We need to rebuild the navigation scheme after adding the first song
                    SetNavigationScheme(true);
                }

                ToastManager.ToastSuccess(Localize.Key("Menu.MusicLibrary.AddedToSet"));
            }
        }

        private void OnSetlistStartButton(NavigationContext ctx) {
            var holdContext = _heldInputs.FirstOrDefault(i => i.Context.IsSameAs(ctx));

            if (ctx.Action == MenuAction.Yellow && (holdContext?.Timer > 0 || ctx.Player is null))
            {
                _heldInputs.RemoveAll(i => i.Context.IsSameAs(ctx));
                if (CurrentSelection is PlaylistViewType playlist)
                {
                    if (playlist.Playlist.SongHashes.Count == 0)
                    {
                        ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.EmptyPlaylist"));
                        return;
                    }

                    if (playlist.Playlist.Ephemeral)
                    {
                        // No, we won't add the setlist to itself, thanks
                        ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.CannotAddToSelf"));
                        return;
                    }

                    var i = 0;

                    foreach (var song in playlist.Playlist.ToList())
                    {
                        ShowPlaylist.AddSong(song);
                        i++;
                    }

                    if (i > 0)
                    {
                        ToastManager.ToastSuccess(Localize.KeyFormat("Menu.MusicLibrary.PlaylistAddedToSet", i));
                    }
                    else
                    {
                        ToastManager.ToastWarning(Localize.Key("Menu.MusicLibrary.NoSongsInPlaylist"));
                    }

                    if (i > 0 && ShowPlaylist.Count == i)
                    {
                        // We need to rebuild the navigation scheme the first time we add song(s)
                        SetNavigationScheme(true);
                    }

                    // If we are in the playlist view, we need to refresh the view
                    if (MenuState == MenuState.PlaylistSelect)
                    {
                        RefreshAndReselect();
                    }

                    return;
                }

                if (CurrentSelection is SongViewType selection)
                {
                    ShowPlaylist.AddSong(selection.SongEntry);
                    if (ShowPlaylist.Count == 1)
                    {
                        // We need to rebuild the navigation scheme after adding the first song
                        SetNavigationScheme(true);
                    }

                    ToastManager.ToastSuccess(Localize.Key("Menu.MusicLibrary.AddedToSet"));
                }
            }
            else
            {
                _heldInputs.RemoveAll(i => i.Context.IsSameAs(ctx));
                if (ShowPlaylist.Count > 0)
                {
                    GlobalVariables.State.PlayingAShow = true;
                    GlobalVariables.State.ShowSongs = ShowPlaylist.ToList();
                    GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs.First();
                    GlobalVariables.State.ShowIndex = 0;
                    MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
                }
            }
        }

        private void StartSetlist()
        {
            if (ShowPlaylist.Count > 0)
            {
                GlobalVariables.State.PlayingAShow = true;
                GlobalVariables.State.ShowSongs = ShowPlaylist.ToList();
                GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs.First();
                GlobalVariables.State.ShowIndex = 0;
                MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
            }
            else
            {
                ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.EmptyPlaylist"));
            }
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

        public void RefreshAndReselect()
        {
            int index = SelectedIndex;
            Refresh();
            SelectedIndex = index;
        }

        public void RefreshSidebar()
        {
            _sidebar.RefreshFavoriteState();
        }

        public void ChangeSort(SortAttribute sort)
        {
            // Keep the previous sort attribute, too, so it can be used to
            // sort the list of unplayed songs and possibly for other things
            if (sort != SortAttribute.Playcount)
            {
                SettingsManager.Settings.PreviousLibrarySort = sort;
            }
            SettingsManager.Settings.LibrarySort = sort;
            UpdateSearch(true);
            UpdateSortInformationHeader();
        }

        private void UpdateSortInformationHeader()
        {
            if (MenuState == MenuState.Library)
            {
                if (SettingsManager.Settings.LibrarySort <= SortAttribute.Playcount)
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
                    var playableSongs = TextColorer.StyleString(Localize.Key("Menu.MusicLibrary.PlayableSongs"),
                        MenuData.Colors.HeaderTertiary,
                        600);

                    var on = TextColorer.StyleString(" ON ",
                        MenuData.Colors.HeaderTertiary,
                        600);

                    var sortKey = TextColorer.StyleString(SettingsManager.Settings.LibrarySort.ToLocalizedName(),
                        MenuData.Colors.HeaderSecondary,
                        700);

                    _sortInfoHeaderPrimaryText.text = ZString.Concat(playableSongs, on, sortKey);
                }


                var count = TextColorer.StyleString(
                    ZString.Format("{0:N0}", _totalSongCount),
                    MenuData.Colors.HeaderSecondary,
                    500);

                var songs = TextColorer.StyleString(
                    _totalSongCount == 1 ? "SONG" : "SONGS",
                    MenuData.Colors.HeaderTertiary,
                    600);

                _sortInfoHeaderSongCountText.text = ZString.Concat(count, " ", songs);

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
                _sortInfoHeaderSongCountText.text = "";
                _sortInfoHeaderStarCountText.text = "";
                _sortInfoHeaderStarIcon.color = _sortInfoHeaderStarIcon.color.WithAlpha(0);
            }

        }

        public void SetSearchInput(SortAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
        }
    }
}