using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
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

    public class MusicLibraryMenu : ListMenu<ViewType, SongView>
    {
        public static MusicLibraryMode LibraryMode;

        public static SongAttribute Sort { get; private set; } = SongAttribute.Name;

        private static List<SongMetadata>? _recommendedSongs;
        private static string _currentSearch = string.Empty;
        private static int _savedIndex;
        private static bool _doRefresh = true;

        public static void SetRefresh()
        {
            _doRefresh = true;
        }

        [Space]
        [SerializeField]
        private TMP_InputField _searchField;
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

        private readonly SongSearching _searchContext = new();
        private IReadOnlyList<SongCategory> _sortedSongs;
        

        private PreviewContext _previewContext;
        private CancellationTokenSource _previewCanceller = new();

        private SongMetadata _currentSong;

        private bool _searchNavPushed;
        private bool _wasSearchFieldFocused;

        protected override void Awake()
        {
            base.Awake();

            // Initialize sidebar
            _sidebar.Initialize(this);
        }

        private void OnEnable()
        {
            // Set up preview context
            _previewContext = new(GlobalVariables.AudioManager);

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
                new NavigationScheme.Entry(MenuAction.Orange, "More Options",
                    () => _popupMenu.gameObject.SetActive(true)),
            }, false));

            // Restore search
            _searchField.text = _currentSearch;

            // Get songs
            if (_doRefresh)
            {
                Refresh();
                _doRefresh = false;
            }
            else
            {
                UpdateSearch(true);
                // Restore index
                SelectedIndex = _savedIndex;
            }

            // Set proper text
            _subHeader.text = LibraryMode switch
            {
                MusicLibraryMode.QuickPlay => "Quickplay",
                MusicLibraryMode.Practice => "Practice",
                _ => throw new Exception("Unreachable.")
            };

            // Set IsPractice as well
            GlobalVariables.Instance.IsPractice = LibraryMode == MusicLibraryMode.Practice;

            // Show no player warning
            _noPlayerWarning.SetActive(PlayerContainer.Players.Count <= 0);
        }

        protected override void OnSelectedIndexChanged()
        {
            base.OnSelectedIndexChanged();

            _sidebar.UpdateSidebar();

            if (CurrentSelection is SongViewType song)
            {
                if (song.SongMetadata == _currentSong)
                {
                    return;
                }

                _currentSong = song.SongMetadata;
            }
            else
            {
                _currentSong = null;
            }

            // Cancel the active song preview
            if (!_previewCanceller.IsCancellationRequested)
            {
                _previewCanceller.Cancel();
            }
        }

        protected override List<ViewType> CreateViewList()
        {
            var list = new List<ViewType>();

            // Return if there are no songs (or they haven't loaded yet)
            if (_sortedSongs is null || GlobalVariables.Instance.SongContainer.Count <= 0) return list;

            // Get the number of songs
            int count = _sortedSongs.Sum(section => section.Songs.Count);

            // Return if there are no songs that match the search criteria
            if (count == 0) return list;

            // Foreach section in the sorted songs...
            foreach (var section in _sortedSongs)
            {
                // Create header
                var displayName = section.Category;
                if (Sort == SongAttribute.Source)
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

            if (!string.IsNullOrEmpty(_searchField.text))
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
                var songContainer = GlobalVariables.Instance.SongContainer;

                // Add "ALL SONGS" header right above the songs
                list.Insert(0,
                    new CategoryViewType("ALL SONGS", songContainer.Count, songContainer.Songs));

                if (_recommendedSongs != null)
                {
                    // Add the recommended songs right above the "ALL SONGS" header
                    list.InsertRange(0, _recommendedSongs.Select(i => new SongViewType(this, i)));
                    list.Insert(0, new CategoryViewType(
                        _recommendedSongs.Count == 1 ? "RECOMMENDED SONG" : "RECOMMENDED SONGS",
                        _recommendedSongs.Count, _recommendedSongs
                    ));
                }

                // Add the buttons
                list.Insert(0, new ButtonViewType("RANDOM SONG", "Icon/Random", SelectRandomSong));
            }

            return list;
        }

        private void SetRecommendedSongs()
        {
            if (GlobalVariables.Instance.SongContainer.Count > 5)
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
            _currentSearch = _searchField.text = string.Empty;
            _sortedSongs = _searchContext.Refresh(Sort);

            SetRecommendedSongs();
            RequestViewListUpdate();

            if (_currentSong == null || !SetIndexTo(i => i is SongViewType view && view.SongMetadata.Directory == _currentSong.Directory))
            {
                SelectedIndex = 2;
            }
        }

        private void UpdateSearch(bool force)
        {
            if (!force && _currentSearch == _searchField.text) return;

            _sortedSongs = _searchContext.Search(_searchField.text, Sort);

            RequestViewListUpdate();

            if (_searchField.text.Length > _currentSearch.Length ||
                !SetIndexTo(i => i is SongViewType view && view.SongMetadata == _currentSong))
            {
                SelectedIndex = _searchContext.IsUnspecified() || _sortedSongs.Count == 1 ? 1 : 2;
            }
            _currentSearch = _searchField.text;
        }

        protected override void Update()
        {
            base.Update();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _searchField.text = string.Empty;
            }

            StartPreview();

            // Update the search bar pushing the empty navigation scheme.
            // We can't use the "OnSelect" event because for some reason it isn't called
            // if the user reselected the input field after pressing enter.
            if (_wasSearchFieldFocused != _searchField.isFocused)
            {
                _wasSearchFieldFocused = _searchField.isFocused;

                if (_wasSearchFieldFocused)
                {
                    if (_searchNavPushed) return;

                    _searchNavPushed = true;
                    Navigator.Instance.PushScheme(NavigationScheme.Empty);
                }
                else
                {
                    if (!_searchNavPushed) return;

                    _searchNavPushed = false;
                    Navigator.Instance.PopScheme();
                }
            }
        }

        private void StartPreview()
        {
            if (_previewContext.IsPlaying || CurrentSelection is not SongViewType song) return;

            _previewCanceller = new();
            float previewVolume = SettingsManager.Settings.PreviewVolume.Value;
            _previewContext.PlayPreview(song.SongMetadata, previewVolume, _previewCanceller.Token).Forget();
        }

        private void OnDisable()
        {
            if (Navigator.Instance == null) return;

            // Save index
            _savedIndex = SelectedIndex;

            Navigator.Instance.PopScheme();

            // Make sure to also pop the search nav if that was pushed
            if (_searchNavPushed)
            {
                Navigator.Instance.PopScheme();
                _searchNavPushed = false;
            }

            // Cancel the preview
            if (!_previewCanceller.IsCancellationRequested)
            {
                _previewCanceller.Cancel();
            }

            _previewContext = null;
        }

        public void SetSearchInput(string query)
        {
            _searchField.text = query;
        }

        private void Back()
        {
            bool searchBoxHasContent = !string.IsNullOrEmpty(_searchField.text);

            if (searchBoxHasContent)
            {
                _searchField.text = string.Empty;
                UpdateSearch(true);
                return;
            }

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

        public void ChangeSort(SongAttribute sort)
        {
            Sort = sort;
            UpdateSearch(true);
        }

        public IEnumerable<(ViewType, int)> GetSections()
        {
            return ViewList.Select((v, i) => (v, i)).Where(i => i.v is SortHeaderViewType);
        }
    }
}