using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
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

        private SongCategory[] _sortedSongs;

        private CancellationTokenSource _previewCanceller;
        private PreviewContext _previewContext;
        private double _previewDelay;

        private SongEntry _currentSong;

        private List<int> _sectionHeaderIndices = new();
        private List<HoldContext> _heldInputs = new();

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
                new NavigationScheme.Entry(MenuAction.Up, "Up",
                    ctx =>
                    {
                        if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange))
                            GoToPreviousSection();
                        else
                            SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Down",
                    ctx =>
                    {
                        if (IsButtonHeldByPlayer(ctx.Player, MenuAction.Orange))
                            GoToNextSection();
                        else
                            SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Confirm",
                    () => CurrentSelection?.PrimaryButtonClick()),

                new NavigationScheme.Entry(MenuAction.Red, "Back", Back),
                new NavigationScheme.Entry(MenuAction.Blue, "Search",
                    () => _searchField.Focus()),
                new NavigationScheme.Entry(MenuAction.Orange, "<align=left><size=80%>More Options<br>(Hold) Navigate Sections</size></align>",
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
                MusicLibraryMode.QuickPlay => "Quickplay",
                MusicLibraryMode.Practice  => "Practice",
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
                if (CurrentlyPlaying == null && song.SongEntry == _currentSong && (_previewCanceller == null || !_previewCanceller.IsCancellationRequested))
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
            if (SelectedPlaylist is not null)
            {
                return CreatePlaylistViewList();
            }

            return CreateNormalViewList();
        }

        private List<ViewType> CreateNormalViewList()
        {
            var list = new List<ViewType>();

            // Return if there are no songs (or they haven't loaded yet)
            if (_sortedSongs is null || SongContainer.Count <= 0) return list;

            // Get the number of songs
            int count = _sortedSongs.Sum(section => section.Songs.Length);

            // Return if there are no songs that match the search criteria
            if (count == 0)
            {
                list.Add(new SortHeaderViewType("NO SONGS MATCH CRITERIA", 0));
                return list;
            }

            // Foreach section in the sorted songs...
            foreach (var section in _sortedSongs)
            {
                // Create header
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
                list.Add(new SortHeaderViewType(displayName, section.Songs.Length));

                // Add all of the songs
                list.AddRange(section.Songs.Select(song => new SongViewType(this, song)));
            }

            if (_searchField.IsSearching)
            {
                // If the current search is NOT empty...

                // Create the category
                var categoryView = new CategoryViewType("SEARCH RESULTS", count, _sortedSongs);

                if (_sortedSongs.Length == 1)
                {
                    // If there is only one header, just replace it
                    list[0] = categoryView;
                }
                else
                {
                    // Otherwise add to the very top
                    list.Insert(0, categoryView);
                }
            }
            else
            {
                if (SettingsManager.Settings.LibrarySort < SortAttribute.Playable)
                {
                    // Add "ALL SONGS" header right above the songs
                    list.Insert(0, new CategoryViewType("ALL SONGS", SongContainer.Count, SongContainer.Songs));

                    if (_recommendedSongs != null)
                    {
                        // Add the recommended songs right above the "ALL SONGS" header

                        list.InsertRange(0, _recommendedSongs.Select(i => new SongViewType(this, i)));
                        list.Insert(0, new CategoryViewType(
                            _recommendedSongs.Length == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
                            _recommendedSongs.Length, _recommendedSongs,
                            () =>
                            {
                                SetRecommendedSongs();
                                RefreshAndReselect();
                            }
                        ));
                    }
                }
                else
                {
                    list.Insert(0, new CategoryViewType("PLAYABLE SONGS", count, _sortedSongs));
                }

                // Add the buttons

                list.Insert(0, new ButtonViewType("RANDOM SONG", "MusicLibraryIcons[Random]",
                    SelectRandomSong, RANDOM_SONG_ID));

                list.Insert(1, new ButtonViewType("PLAYLISTS", "MusicLibraryIcons[Playlists]", () =>
                {
                    // TODO: Proper playlist menu
                    SelectedPlaylist = PlaylistContainer.FavoritesPlaylist;
                    Refresh();
                }, PLAYLIST_ID));
            }

            CalculateCategoryHeaderIndices(list);
            return list;
        }

        private List<ViewType> CreatePlaylistViewList()
        {
            var list = new List<ViewType>();

            // Add back button
            list.Add(new ButtonViewType("BACK", "MusicLibraryIcons[Back]", ExitPlaylistTab, BACK_ID));

            // Return if there are no songs (or they haven't loaded yet)
            if (_sortedSongs is null || SongContainer.Count <= 0) return list;

            // Get the number of songs
            int count = _sortedSongs.Sum(section => section.Songs.Length);

            // Return if there are no songs in the playlist
            if (count == 0) return list;

            // Add all of the songs
            foreach (var section in _sortedSongs)
            {
                // Create header
                var displayName = section.Category;
                list.Add(new SortHeaderViewType(displayName.ToUpperInvariant(), section.Songs.Length));

                // Add all of the songs
                list.AddRange(section.Songs.Select(song => new SongViewType(this, song)));
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
            _sectionHeaderIndices = list.Select((v, i) => (v, i)).
                Where(viewTypeEntry => viewTypeEntry.v is SortHeaderViewType or CategoryViewType).
                Select(viewTypeEntry => viewTypeEntry.i).ToList();
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
                // If there's no playlist selected...
                if (SettingsManager.Settings.LibrarySort == SortAttribute.Playable)
                {
                    if (PlayerContainer.Players.Count == 0)
                    {
                        SettingsManager.Settings.LibrarySort = SortAttribute.Name;
                    }
                }
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
                    new(SelectedPlaylist.Name, songs[..count])
                };

                _searchField.gameObject.SetActive(false);
            }

            RequestViewListUpdate();

            if (_reloadState != MusicLibraryReloadState.Partial)
            {
                if (_searchField.IsUpdatedSearchLonger || _currentSong == null || !SetIndexTo(i => i is SongViewType view && view.SongEntry.Directory == _currentSong.Directory))
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

            var context = await PreviewContext.Create(_currentSong, previewVolume, GlobalVariables.State.SongSpeed, delay, FADE_DURATION, canceller);
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

        public IEnumerable<(ViewType, int)> GetSections()
        {
            return ViewList.Select((v, i) => (v, i)).Where(i => i.v is SortHeaderViewType);
        }

        public void SetSearchInput(SortAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
        }
    }
}