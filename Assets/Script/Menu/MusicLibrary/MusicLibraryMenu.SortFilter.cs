using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Playlists;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public partial class MusicLibraryMenu
    {
#nullable enable
        private static SongEntry[]? _recommendedSongs;
#nullable disable

        private static string _currentSearch = string.Empty;

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

        public bool HasSortHeaders { get; private set; }

        private SongCategory[] _sortedSongs;
        private SortAttribute _playlistSort = SortAttribute.Name;
        private bool _playlistSortAscending = true;

        private List<int> _sectionHeaderIndices = new();
        private int _primaryHeaderIndex;
        private int _recommendedHeaderIndex = -1;

        private void CalculateCategoryHeaderIndices(List<ViewType> list)
        {
            _sectionHeaderIndices.Clear();
            Shortcuts.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry is CategoryViewType or ButtonViewType)
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
                    new(GetPlaylistDisplayName(SelectedPlaylist), songs[..count], null)
                };

                _searchField.gameObject.SetActive(false);
            }

            string currentSearch = _searchField.FullSearchQuery;
            bool searchChanged = !PlaylistMode &&
                !string.Equals(previousSearch, currentSearch, StringComparison.Ordinal);
            bool searchExpanded = !PlaylistMode && currentSearch.Length > previousSearch.Length;
            _currentSearch = currentSearch;
            _searchField.UpdateSearchText();

            var predicate = YARG.Menu.Filters.FiltersMenu.ActiveFilterPredicate;
            bool inLibrary = !PlaylistMode && MenuState == MenuState.Library;
            bool shouldApplyFilters = inLibrary && predicate != null;
            bool shouldShowFilteredCounts = inLibrary && (_searchField.IsSearching || predicate != null);

            if (shouldApplyFilters) {
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

            if (_reloadState != MusicLibraryReloadState.Partial && !searchChanged &&
                MenuState != MenuState.PlaylistSelect &&
                !_forceGoToCurrentlyPlaying)
            {
                int newPositionStartIndex = _recommendedHeaderIndex != -1 ? _primaryHeaderIndex : 0;

                if (_currentSong == null ||
                    !SetIndexTo(i => i is SongViewType view &&
                        view.SongEntry.SortBasedLocation == _currentSong.SortBasedLocation,
                        newPositionStartIndex))
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

        private bool SetIndexToFirstRecommendedSong()
        {
            if (_recommendedSongs == null || _recommendedSongs.Length == 0)
                return false;

            var recommendedSet = new HashSet<SongEntry>(_recommendedSongs);
            return SetIndexTo(i => i is SongViewType view && recommendedSet.Contains(view.SongEntry));
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

        public void ApplySortFromPopup(SortAttribute sort, bool ascending = true)
        {
            if (MenuState == MenuState.Playlist && SelectedPlaylist != null)
            {
                switch (sort)
                {
                    case SortAttribute.Name:
                        SelectedPlaylist.SortByName(ascending);
                        break;
                    case SortAttribute.Artist:
                        SelectedPlaylist.SortByArtist(ascending);
                        break;
                    default:
                        ToastManager.ToastWarning("Sort not supported in playlists");
                        return;
                }

                _playlistSort = sort;
                _playlistSortAscending = ascending;
                RefreshAndReselect();
                return;
            }

            ChangeSort(sort);
        }

        public SortAttribute GetPopupSortAttribute()
        {
            return MenuState == MenuState.Playlist ? _playlistSort : SettingsManager.Settings.LibrarySort;
        }

        public string GetPopupSortLabel()
        {
            var sort = GetPopupSortAttribute().ToLocalizedName();
            if (MenuState != MenuState.Playlist)
            {
                return sort;
            }

            return _playlistSortAscending ? $"{sort} (A-Z)" : $"{sort} (Z-A)";
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
                    TextColorer.StyleString(GetPlaylistDisplayName(SelectedPlaylist), MenuData.Colors.HeaderSecondary, 700));

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

        private static string GetPlaylistDisplayName(Playlist playlist)
        {
            if (playlist == null)
                return string.Empty;

            if (playlist.Ephemeral)
                return Localize.Key("Menu.MusicLibrary.CurrentSetlist");

            return playlist.Name;
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
    }
}
