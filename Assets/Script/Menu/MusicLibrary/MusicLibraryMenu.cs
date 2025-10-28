using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Input;
using YARG.Localization;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Multiplayer;
using YARG.Player;
using YARG.Playlists;
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

        private sealed class MenuButtonHold
        {
            public MenuButtonHold(NavigationContext context)
            {
                Context = context;
                Duration = 0f;
                UsedAsModifier = false;
                Triggered = false;
            }

            public NavigationContext Context { get; }
            public float Duration { get; set; }
            public bool UsedAsModifier { get; set; }
            public bool Triggered { get; set; }
        }

        private readonly List<MenuButtonHold> _heldInputs = new();

        private const float MORE_OPTIONS_ACTIVATION_DELAY = 0.35f;
        private float _moreOptionsActivationTimer;

        // Doesn't go through PlaylistContainer because it is ephemeral

        private static Instrument _lastInstrument;
        private static Difficulty _lastDifficulty;

        private static bool _needsReload = false;

        public static void NeedsReload()
        {
            _needsReload = true;
        }

        private int _primaryHeaderIndex;

        // Multiplayer song queue integration
        private YARG.Multiplayer.MultiplayerSongQueue _songQueue;

        private void EnsureSongQueue()
        {
            if (_songQueue == null)
            {
                _songQueue = FindObjectOfType<YARG.Multiplayer.MultiplayerSongQueue>();
                if (_songQueue == null && YARG.Networking.YargNetworkManager.Instance != null && YARG.Networking.YargNetworkManager.Instance.isNetworkActive)
                {
                    var go = new GameObject("MultiplayerSongQueue");
                    _songQueue = go.AddComponent<YARG.Multiplayer.MultiplayerSongQueue>();
                }
            }
        }

        // Add a song to the multiplayer queue (host only)
        public void AddSongToMultiplayerQueue(string songId, string songName)
        {
            EnsureSongQueue();
            if (_songQueue != null && _songQueue.isServer)
            {
                var entry = new YARG.Multiplayer.SongQueueEntry { songId = songId, songName = songName };
                _songQueue.AddSongToQueue(entry);
            }
        }

        // Remove a song from the multiplayer queue (host only)
        public void RemoveSongFromMultiplayerQueue(int index)
        {
            EnsureSongQueue();
            if (_songQueue != null && _songQueue.isServer)
            {
                _songQueue.RemoveSongFromQueue(index);
            }
        }

        // Clear the multiplayer queue (host only)
        public void ClearMultiplayerQueue()
        {
            EnsureSongQueue();
            if (_songQueue != null && _songQueue.isServer)
            {
                _songQueue.ClearQueue();
            }
        }

        // Start the set (host only)
        public void StartMultiplayerSet()
        {
            EnsureSongQueue();
            if (_songQueue != null && _songQueue.isServer)
            {
                _songQueue.StartSet();
            }
        }

        // Get the current queue (all clients)
        public IReadOnlyList<YARG.Multiplayer.SongQueueEntry> GetMultiplayerQueue()
        {
            EnsureSongQueue();
            return _songQueue?.Queue ?? new List<YARG.Multiplayer.SongQueueEntry>();
        }

        // Get current song index (all clients)
        public int GetCurrentQueueSongIndex()
        {
            EnsureSongQueue();
            return _songQueue?.currentSongIndex ?? -1;
        }

        // Is set active (all clients)
        public bool IsMultiplayerSetActive()
        {
            EnsureSongQueue();
            return _songQueue?.isSetActive ?? false;
        }

        protected override void Awake()
        {
            base.Awake();

            // Initialize sidebar
            _sidebar.Initialize(this, _searchField);
        }

        private void OnEnable()
        {
            // Set navigation scheme
            SetNavigationScheme();

            // Initialize multiplayer show playlist if in multiplayer
            if (Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                EnsureMultiplayerShowPlaylist();
            }

            // Restore search
            _searchField.Restore();
            _searchField.OnSearchQueryUpdated += UpdateSearch;
            MultiplayerSongFilter.SharedSongsUpdated += OnSharedSongsUpdated;

            if (CurrentlyPlaying != null)
            {
                _currentSong = CurrentlyPlaying;
            }

            ShouldDisplaySoloHighScores = !PlayerContainer.OnlyHasBotsActive();

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

            PlayerContainer.PlayerAdded += OnPlayerAdded;
            PlayerContainer.PlayerRemoved += OnPlayerRemoved;
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
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            if (MenuState != MenuState.Show)
            {
                _moreOptionsActivationTimer = 0f;
            }

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

            // Check if we're in multiplayer mode
            bool isMultiplayer = Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive;
            
            if (ShowPlaylist.Count == 0)
            {
                // Yellow button behavior: in multiplayer it's for quick-start, otherwise add to set
                string yellowLabel = isMultiplayer ? "Menu.MusicLibrary.QuickStart" : "Menu.MusicLibrary.AddToSet";
                System.Action yellowAction = isMultiplayer ? (System.Action)QuickStartShow : AddToPlaylist;
                
                Navigator.Instance.PushScheme(new NavigationScheme(new()
                {
                    new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                        ctx =>
                        {
                            if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange, true))
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
                            if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange, true))
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
                    new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                        () => CurrentSelection?.PrimaryButtonClick()),
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                    new NavigationScheme.Entry(MenuAction.Yellow, yellowLabel, yellowAction),
                    new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.PlayShow",
                        EnterShowMode),
                    new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                        OnButtonHit, OnButtonRelease),
                }, false));
            }
            else
            {
                // Yellow button behavior: in multiplayer it's for quick-start, otherwise add to set
                string yellowLabel = isMultiplayer ? "Menu.MusicLibrary.QuickStart" : "Menu.MusicLibrary.AddToSet";
                System.Action yellowAction = isMultiplayer ? (System.Action)QuickStartShow : AddToPlaylist;
                
                // Blue button behavior: in multiplayer go to setlist management, in single player start show
                string blueLabel = isMultiplayer ? "Menu.MusicLibrary.ViewSetlist" : "Menu.MusicLibrary.StartSet";
                System.Action blueAction = isMultiplayer ? (System.Action)EnterShowMode : StartSetlist;
                
                Navigator.Instance.PushScheme(new NavigationScheme(new()
                {
                    new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                        ctx =>
                        {
                            if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange, true))
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
                            if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange, true))
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
                    new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                        () => CurrentSelection?.PrimaryButtonClick()),
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                    new NavigationScheme.Entry(MenuAction.Yellow, yellowLabel, yellowAction),
                    new NavigationScheme.Entry(MenuAction.Blue, blueLabel, blueAction),
                    new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                        OnButtonHit, OnButtonRelease),
                }, false));
            }
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

                if (SettingsManager.Settings.LibrarySort < SortAttribute.Playable)
                {
                    list.Add(new CategoryViewType(
                        Localize.Key("Menu.MusicLibrary.AllSongs"), songCount, SongContainer.Songs));

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
                else
                {
                    list.Add(new CategoryViewType(Localize.Key("Menu.MusicLibrary.PlayableSongs"), songCount, _sortedSongs));
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

                if (_sortedSongs.Length > 1)
                {
                    list.Add(new SortHeaderViewType(displayName, section.Songs.Length, section.CategoryGroup));
                }

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

        private void ExitLibrary()
        {
            ShowPlaylist.Clear();
            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            _previewContext = null;
            StemSettings.ApplySettings = true;
            
            // Sync menu navigation in multiplayer
            if (Networking.YargNetworkManager.Instance != null && 
                Networking.YargNetworkManager.Instance.isNetworkActive &&
                Networking.YargNetworkManager.Instance.IsHosting)
            {
                Debug.Log("[MusicLibraryMenu] Host exiting library - syncing to clients");
                Networking.YargNetworkManager.Instance.SyncMenuNavigation(popMenu: true);
                Debug.Log("[MusicLibraryMenu] Sync complete, now popping menu locally");
                
                // If navigation stack is incomplete (< 4 menus), we came from gameplay after disconnect
                // In this case, close the lobby directly since LobbyRoom isn't in the stack
                if (MenuManager.Instance != null && !MenuManager.Instance.IsMenuInStack(MenuManager.Menu.LobbyRoom))
                {
                    Debug.Log("[MusicLibraryMenu] LobbyRoom menu missing from stack - closing lobby directly");
                    Networking.YargNetworkManager.Instance.LeaveLobby();
                }
            }
            
            Debug.Log($"[MusicLibraryMenu] Calling PopMenu - MenuManager.Instance null? {MenuManager.Instance == null}");
            MenuManager.Instance.PopMenu();
            Debug.Log("[MusicLibraryMenu] PopMenu complete");
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
                var songs = RecommendedSongs.GetRecommendedSongs();
                if (songs.Length > 0)
                {
                    var filtered = MultiplayerSongFilter.FilterSongs(songs);
                    _recommendedSongs = filtered.Length > 0 ? filtered : null;
                }
                else
                {
                    _recommendedSongs = null;
                }
            }
            else
            {
                _recommendedSongs = null;
            }
        }

        private void ApplyMultiplayerSongFilter()
        {
            if (_sortedSongs == null)
            {
                return;
            }

            _sortedSongs = MultiplayerSongFilter.FilterCategories(_sortedSongs);
        }

        private void OnSharedSongsUpdated()
        {
            SetReload(MusicLibraryReloadState.Partial);

            if (!isActiveAndEnabled)
            {
                return;
            }

            SetRecommendedSongs();
            UpdateSearch(true);
        }

        private void Refresh()
        {
            SetRecommendedSongs();
            _searchField.Reset();
            UpdateSearch(true);
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

            ApplyMultiplayerSongFilter();

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
            {
                heldInput.Duration += Time.unscaledDeltaTime;

                if (heldInput.Context.Player == null && heldInput.Context.Action == MenuAction.Orange && !heldInput.Triggered)
                {
                    if (_moreOptionsActivationTimer <= 0f)
                    {
                        if (!_popupMenu.gameObject.activeSelf)
                        {
                            _popupMenu.gameObject.SetActive(true);
                        }

                        heldInput.Triggered = true;
                    }
                }
            }

            if (_moreOptionsActivationTimer > 0f)
            {
                _moreOptionsActivationTimer -= Time.unscaledDeltaTime;
            }

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
            MultiplayerSongFilter.SharedSongsUpdated -= OnSharedSongsUpdated;

            PlayerContainer.PlayerAdded -= OnPlayerAdded;
            PlayerContainer.PlayerRemoved -= OnPlayerRemoved;

            _heldInputs.Clear();

            // Unsubscribe from multiplayer playlist updates
            if (_multiplayerShowPlaylist != null)
            {
                _multiplayerShowPlaylist.OnPlaylistUpdated -= OnMultiplayerPlaylistUpdated;
            }
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
                case MenuState.Show:
                    LeaveShowMode();
                    break;
                case MenuState.Library:
                    ExitLibrary();
                    break;
            }
        }

        private bool IsButtonHeldByPlayer(YargPlayer player, MenuAction button, bool markAsModifier = false)
        {
            foreach (var hold in _heldInputs)
            {
                if (hold.Context.Player == player && hold.Context.Action == button)
                {
                    if (markAsModifier)
                    {
                        hold.UsedAsModifier = true;
                    }

                    return true;
                }
            }

            return false;
        }

        private void OnButtonHit(NavigationContext ctx)
        {
            _heldInputs.Add(new MenuButtonHold(ctx));
        }

        private void OnButtonRelease(NavigationContext ctx)
        {
            var holdContext = _heldInputs.FirstOrDefault(i => i.Context.IsSameAs(ctx));

            if (ctx.Action == MenuAction.Orange && holdContext != null)
            {
                bool triggeredByInstrument = ctx.Player != null;
                bool usedAsModifier = holdContext.UsedAsModifier;

                if (!holdContext.Triggered && _moreOptionsActivationTimer <= 0f && (!triggeredByInstrument || !usedAsModifier))
                {
                    if (!_popupMenu.gameObject.activeSelf)
                    {
                        _popupMenu.gameObject.SetActive(true);
                    }

                    holdContext.Triggered = true;
                }
            }

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

        public void RefreshAndReselect()
        {
            int index = SelectedIndex;
            Refresh();
            SelectedIndex = index;
        }

        public void ChangeSort(SortAttribute sort)
        {
            // Keep the previous sort attribute, too, so it can be used to
            // sort the list of unplayed songs and possibly for other things
            if (sort != SortAttribute.Playcount && sort != SortAttribute.Stars)
            {
                SettingsManager.Settings.PreviousLibrarySort = sort;
            }
            SettingsManager.Settings.LibrarySort = sort;
            UpdateSearch(true);
        }

        public void SetSearchInput(SortAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
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