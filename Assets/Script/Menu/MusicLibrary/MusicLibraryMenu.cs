﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Localization;
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

    public class MusicLibraryMenu : ListMenu<ViewType, SongView>
    {
        private const int RANDOM_SONG_ID = 0;
        private const int PLAYLIST_ID = 1;
        private const int BACK_ID = 2;

        public static MusicLibraryMode LibraryMode;

        public static SongEntry CurrentlyPlaying;
        public static Playlist SelectedPlaylist;

#nullable enable
        private static SongEntry[]? _recommendedSongs;
#nullable disable

        private static string _currentSearch = string.Empty;
        private static int _savedIndex;
        private static MusicLibraryReloadState _reloadState = MusicLibraryReloadState.Full;

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

        private SongCategory[] _sortedSongs;

        private CancellationTokenSource _previewCanceller;
        private PreviewContext _previewContext;
        private double _previewDelay;

        private SongEntry _currentSong;

        private List<int> _sectionHeaderIndices = new();
        public List<(string, int)> Shortcuts { get; private set; } = new();

        private List<HoldContext> _heldInputs = new();

        private int _primaryHeaderIndex;

        protected override void Awake()
        {
            base.Awake();

            // Initialize sidebar
            _sidebar.Initialize(this, _searchField);
        }

        private void OnEnable()
        {
            // Set navigation scheme
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
                    () => CurrentSelection?.PrimaryButtonClick()),

                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Search",
                    () => _searchField.Focus()),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                    OnButtonHit, OnButtonRelease),
            }, false));

            // Restore search
            _searchField.Restore();
            _searchField.OnSearchQueryUpdated += UpdateSearch;

            if (CurrentlyPlaying != null)
            {
                _currentSong = CurrentlyPlaying;
            }

            StemSettings.ApplySettings = SettingsManager.Settings.ApplyVolumesInMusicLibrary.Value;
            _previewDelay = 0;
            if (_reloadState == MusicLibraryReloadState.Full)
            {
                Refresh();
            }
            else if (_reloadState == MusicLibraryReloadState.Partial)
            {
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

            // Show no player warning
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);
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

            var viewList = (SelectedPlaylist is not null) ? CreatePlaylistViewList() : CreateNormalViewList();

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

            if (_searchField.IsSearching)
            {
                int count = _sortedSongs.Sum(section => section.Songs.Length);
                list.Add(new CategoryViewType(Localize.Key("Menu.MusicLibrary.SearchResults"), count, _sortedSongs));
            }
            else
            {
                list.Add(new ButtonViewType(Localize.Key("Menu.MusicLibrary.RandomSong"), "MusicLibraryIcons[Random]", SelectRandomSong, RANDOM_SONG_ID));
                list.Add(new ButtonViewType(Localize.Key("Menu.MusicLibrary.Playlists"), "MusicLibraryIcons[Playlists]",
                    () =>
                    {
                        // TODO: Proper playlist menu
                        SelectedPlaylist = PlaylistContainer.FavoritesPlaylist;
                        Refresh();
                    },
                    PLAYLIST_ID));

                _primaryHeaderIndex += 2;

                if (SettingsManager.Settings.LibrarySort < SortAttribute.Playable)
                {
                    list.Add(new CategoryViewType(Localize.Key("Menu.MusicLibrary.AllSongs"), SongContainer.Count, SongContainer.Songs));
                    if (_recommendedSongs != null)
                    {
                        string key = Localize.Key("Menu.MusicLibrary.RecommendedSongs", _recommendedSongs.Length == 1 ? "Singular" : "Plural");
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
                    int count = _sortedSongs.Sum(section => section.Songs.Length);
                    list.Add(new CategoryViewType(Localize.Key("Menu.MusicLibrary.PlayableSongs"), count, _sortedSongs));
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
                    list.Add(new SongViewType(this, song));
                }
            }
            CalculateCategoryHeaderIndices(list);
            return list;
        }

        private List<ViewType> CreatePlaylistViewList()
        {
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"), "MusicLibraryIcons[Back]", ExitPlaylistTab, BACK_ID)
            };

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0 || !_sortedSongs.Any(section => section.Songs.Length > 0))
            {
                return list;
            }

            foreach (var section in _sortedSongs)
            {
                list.Add(new SortHeaderViewType(section.Category.ToUpperInvariant(), section.Songs.Length, section.CategoryGroup));
                foreach (var song in section.Songs)
                {
                    list.Add(new SongViewType(this, song));
                }
            }

            CalculateCategoryHeaderIndices(list);
            return list;
        }

        private void ExitPlaylistTab()
        {
            SelectedPlaylist = null;
            Refresh();

            // Select playlist button
            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
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
            if (SongContainer.Count > 5)
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
        }

        private void UpdateSearch(bool force)
        {
            if (!force && _searchField.IsCurrentSearchInField)
            {
                return;
            }

            if (SelectedPlaylist is null)
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
                    !SetIndexTo(i => i is SongViewType view && view.SongEntry.Directory == _currentSong.Directory, newPositionStartIndex))
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

            // Save index
            _savedIndex = SelectedIndex;

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
            if (SelectedPlaylist is not null)
            {
                ExitPlaylistTab();
                return;
            }

            if (_searchField.IsSearching)
            {
                _searchField.ClearFilterQueries();
                return;
            }

            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            _previewContext = null;
            StemSettings.ApplySettings = true;
            MenuManager.Instance.PopMenu();
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
            SettingsManager.Settings.LibrarySort = sort;
            UpdateSearch(true);
        }

        public void SetSearchInput(SortAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
        }
    }
}