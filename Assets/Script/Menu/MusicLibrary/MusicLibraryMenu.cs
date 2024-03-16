using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
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

    public class MusicLibraryMenu : ListMenu<ViewType, SongView>
    {
        private const int RANDOM_SONG_ID = 0;
        private const int PLAYLIST_ID = 1;
        private const int BACK_ID = 2;

        public static MusicLibraryMode LibraryMode;

        public static SongEntry InitialSelect;
        public static Playlist SelectedPlaylist;

#nullable enable
        private static List<SongEntry>? _recommendedSongs;
#nullable disable

        private static string _currentSearch = string.Empty;
        private static int _savedIndex;
        private static bool _doRefresh = true;

        public static void SetRefresh()
        {
            _doRefresh = true;
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

        private IReadOnlyList<SongCategory> _sortedSongs;

        private readonly object _previewLock = new();
        private CancellationTokenSource _previewCanceller;
        private PreviewContext _previewContext;

        private SongEntry _currentSong;

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
                    () => SelectedIndex--),
                new NavigationScheme.Entry(MenuAction.Down, "Down",
                    () => SelectedIndex++),
                new NavigationScheme.Entry(MenuAction.Green, "Confirm",
                    () => CurrentSelection?.PrimaryButtonClick()),

                new NavigationScheme.Entry(MenuAction.Red, "Back", Back),
                new NavigationScheme.Entry(MenuAction.Blue, "Search",
                    () => _searchField.Focus()),
                new NavigationScheme.Entry(MenuAction.Orange, "More Options",
                    () => _popupMenu.gameObject.SetActive(true)),
            }, false));

            // Restore search
            _searchField.Restore();
            _searchField.OnSearchQueryUpdated += UpdateSearch;

            // Get songs
            if (_doRefresh)
            {
                if (InitialSelect != null)
                {
                    _currentSong = InitialSelect;
                }

                Refresh();
                _doRefresh = false;
            }
            else
            {
                UpdateSearch(true);
                // Restore index
                SelectedIndex = _savedIndex;
            }

            InitialSelect = null;

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
            base.OnSelectedIndexChanged();

            _sidebar.UpdateSidebar();

            if (CurrentSelection is SongViewType song)
            {
                if (song.SongEntry == _currentSong && (_previewContext == null || _previewContext.IsPlaying))
                {
                    return;
                }

                _currentSong = song.SongEntry;
            }
            else
            {
                _currentSong = null;
            }

            CancellationTokenSource canceller;
            lock (_previewLock)
            {
                _previewCanceller?.Cancel();
                _previewContext?.Stop();
                _previewContext = null;
                canceller = _previewCanceller = new CancellationTokenSource();
            }
            StartPreview(canceller);
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
            int count = _sortedSongs.Sum(section => section.Songs.Count);

            // Return if there are no songs that match the search criteria
            if (count == 0) return list;

            // Foreach section in the sorted songs...
            foreach (var section in _sortedSongs)
            {
                // Create header
                var displayName = section.Category;
                if (SettingsManager.Settings.LibrarySort == SongAttribute.Source)
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
                list.Add(new SortHeaderViewType(displayName, section.Songs.Count));

                // Add all of the songs
                list.AddRange(section.Songs.Select(song => new SongViewType(this, song)));
            }

            if (_searchField.IsSearching)
            {
                // If the current search is NOT empty...

                // Create the category
                var categoryView = new CategoryViewType("SEARCH RESULTS", count, _sortedSongs);

                if (_sortedSongs.Count == 1)
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
                // Add "ALL SONGS" header right above the songs
                list.Insert(0, new CategoryViewType("ALL SONGS", SongContainer.Count, SongContainer.Songs));

                if (_recommendedSongs != null)
                {
                    // Add the recommended songs right above the "ALL SONGS" header

                    list.InsertRange(0, _recommendedSongs.Select(i => new SongViewType(this, i)));

                    list.Insert(0, new CategoryViewType(
                        _recommendedSongs.Count == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
                        _recommendedSongs.Count, _recommendedSongs,
                        () =>
                        {
                            SetRecommendedSongs();
                            RefreshAndReselect();
                        }
                    ));
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

            return list;
        }

        private List<ViewType> CreatePlaylistViewList()
        {
            var list = new List<ViewType>();

            // Add back button
            list.Add(new ButtonViewType("BACK", "MusicLibraryIcons[Back]", () =>
            {
                SelectedPlaylist = null;
                Refresh();

                // Select playlist button
                SetIndexTo(i => i is ButtonViewType { Id: PLAYLIST_ID });
            }, BACK_ID));

            // Return if there are no songs (or they haven't loaded yet)
            if (_sortedSongs is null || SongContainer.Count <= 0) return list;

            // Get the number of songs
            int count = _sortedSongs.Sum(section => section.Songs.Count);

            // Return if there are no songs in the playlist
            if (count == 0) return list;

            // Add all of the songs
            foreach (var section in _sortedSongs)
            {
                // Create header
                var displayName = section.Category;
                list.Add(new SortHeaderViewType(displayName.ToUpperInvariant(), section.Songs.Count));

                // Add all of the songs
                list.AddRange(section.Songs.Select(song => new SongViewType(this, song)));
            }

            return list;
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
            UpdateSearch(true, true);
        }

        private void UpdateSearch(bool force)
        {
            UpdateSearch(force, false);
        }

        private void UpdateSearch(bool force, bool refresh)
        {
            if (!force && _searchField.IsCurrentSearchInField)
            {
                return;
            }

            if (SelectedPlaylist is null)
            {
                // If there's no playlist selected...

                if (refresh)
                {
                    _sortedSongs = _searchField.Refresh(SettingsManager.Settings.LibrarySort);
                }
                else
                {
                    _sortedSongs = _searchField.Search(SettingsManager.Settings.LibrarySort);
                }

                _searchField.gameObject.SetActive(true);
            }
            else
            {
                // Show playlist...

                var songs = new List<SongEntry>();
                foreach (var hash in SelectedPlaylist.SongHashes)
                {
                    // Get the first song with the specified hash
                    if (SongContainer.SongsByHash.TryGetValue(hash, out var song))
                    {
                        songs.Add(song[0]);
                    }
                }

                _sortedSongs = new List<SongCategory>
                {
                    new(SelectedPlaylist.Name, songs)
                };

                _searchField.gameObject.SetActive(false);
            }

            RequestViewListUpdate();

            if (_searchField.IsUpdatedSearchLonger ||
                // Try to select the last selected song
                !SetIndexTo(i => i is SongViewType view && view.SongEntry == _currentSong))
            {
                // Try to select the song after the first category
                if (!SetIndexTo(i => i is CategoryViewType, 1))
                {
                    // If all else fails, jump to the first item
                    SelectedIndex = 0;
                }
            }

            _searchField.UpdateSearchText();
        }

        protected override void Update()
        {
            base.Update();
        }

        private async void StartPreview(CancellationTokenSource canceller)
        {
            if (CurrentSelection is not SongViewType song)
            {
                return;
            }

            float previewVolume = SettingsManager.Settings.PreviewVolume.Value;
            var context = await song.SongEntry.LoadPreview(previewVolume, canceller);
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

            _previewContext?.Stop();
            _searchField.OnSearchQueryUpdated -= UpdateSearch;
        }

        private void OnDestroy()
        {
            _previewContext?.Stop();
        }

        private void Back()
        {
            if (_searchField.IsSearching)
            {
                _searchField.ClearFilterQueries();
                return;
            }

            _previewCanceller?.Cancel();
            _previewContext?.Dispose();
            MenuManager.Instance.PopMenu();
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

        public void ChangeSort(SongAttribute sort)
        {
            SettingsManager.Settings.LibrarySort = sort;
            UpdateSearch(true);
        }

        public IEnumerable<(ViewType, int)> GetSections()
        {
            return ViewList.Select((v, i) => (v, i)).Where(i => i.v is SortHeaderViewType);
        }

        public void SetSearchInput(SongAttribute songAttribute, string input)
        {
            _searchField.SetSearchInput(songAttribute, input);
        }
    }
}