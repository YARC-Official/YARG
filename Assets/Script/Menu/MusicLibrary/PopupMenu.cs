using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class PopupMenu : MonoBehaviour
    {
        private enum State
        {
            Main,
            SortSelect,
            GoToSection
        }

        [SerializeField]
        private PopupMenuItem _menuItemPrefab;

        [Space]
        [SerializeField]
        private GameObject _header;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private MusicLibraryMenu _musicLibrary;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;

        private State _menuState;

        private void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", () =>
                {
                    if (_menuState == State.Main)
                    {
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        _menuState = State.Main;
                        UpdateForState();
                    }
                })
            }, false));

            _menuState = State.Main;
            UpdateForState();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void UpdateForState()
        {
            // Reset content
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();

            // Create the menu
            switch (_menuState)
            {
                case State.Main:
                    CreateMainMenu();
                    break;
                case State.SortSelect:
                    CreateSortSelect();
                    break;
                case State.GoToSection:
                    CreateGoToSection();
                    break;
            }

            _navGroup.SelectFirst();
        }

        private void CreateMainMenu()
        {
            SetHeader(null);

            CreateItem("Random Song", () =>
            {
                _musicLibrary.SelectRandomSong();
                gameObject.SetActive(false);
            });

            CreateItem("Back To Top", () =>
            {
                _musicLibrary.SelectedIndex = 0;
                gameObject.SetActive(false);
            });

            CreateItem("Sort By: " + SettingsManager.Settings.LibrarySort.ToLocalizedName(), () =>
            {
                _menuState = State.SortSelect;
                UpdateForState();
            });

            CreateItem("Go To Section...", () =>
            {
                _menuState = State.GoToSection;
                UpdateForState();
            });

            var viewType = _musicLibrary.CurrentSelection;

            // Add/remove to favorites
            var favoriteInfo = viewType.GetFavoriteInfo();
            if (favoriteInfo.ShowFavoriteButton)
            {
                if (!favoriteInfo.IsFavorited)
                {
                    CreateItem("Add To Favorites", () =>
                    {
                        viewType.FavoriteClick();
                        _musicLibrary.RefreshViewsObjects();

                        gameObject.SetActive(false);
                    });
                }
                else
                {
                    CreateItem("Remove From Favorites", () =>
                    {
                        viewType.FavoriteClick();
                        _musicLibrary.RefreshViewsObjects();

                        gameObject.SetActive(false);
                    });
                }
            }

            // Only show these options if we are selecting a song
            if (viewType is SongViewType songViewType &&
                SettingsManager.Settings.ShowAdvancedMusicLibraryOptions.Value)
            {
                var song = songViewType.SongEntry;

                CreateItem("View Song Folder", () =>
                {
                    FileExplorerHelper.OpenFolder(song.Directory);

                    gameObject.SetActive(false);
                });

                CreateItem("Copy Song Checksum", () =>
                {
                    GUIUtility.systemCopyBuffer = song.Hash.ToString();

                    gameObject.SetActive(false);
                });
            }
        }

        private void CreateSortSelect()
        {
            SetHeader("Sort By...");

            foreach (var sort in EnumExtensions<SortAttribute>.Values)
            {
                // Skip theses because they don't make sense
                if (sort == SortAttribute.Unspecified)
                    continue;

                if (sort == SortAttribute.Playable && PlayerContainer.Players.Count == 0)
                    continue;

                if (sort >= SortAttribute.Instrument)
                    break;

                CreateItem(sort.ToLocalizedName(), () =>
                {
                    _musicLibrary.ChangeSort(sort);
                    gameObject.SetActive(false);
                });
            }

            foreach (var instrument in EnumExtensions<Instrument>.Values)
            {
                if (SongContainer.HasInstrument(instrument))
                {
                    var attribute = instrument.ToSortAttribute();
                    CreateItem(attribute.ToLocalizedName(), () =>
                    {
                        _musicLibrary.ChangeSort(attribute);
                        gameObject.SetActive(false);
                    });
                }
            }
        }

        private void CreateGoToSection()
        {
            SetHeader("Go To...");

            if (SettingsManager.Settings.LibrarySort
                is SortAttribute.Artist
                or SortAttribute.Album
                or SortAttribute.Artist_Album)
            {
                foreach (var (header, index) in _musicLibrary.GetSections()
                    .GroupBy(x => SortString.RemoveArticle(((SortHeaderViewType) x.Item1).HeaderText)[0].ToAsciiUpper())
                    .Select(g => g.First()))
                {
                    CreateItem(SortString.RemoveArticle(((SortHeaderViewType) header).HeaderText)[0].ToString(), () =>
                    {
                        _musicLibrary.SelectedIndex = index;
                        gameObject.SetActive(false);
                    });
                }
            }
            else
            {
                foreach (var (header, index) in _musicLibrary.GetSections())
                {
                    CreateItem(((SortHeaderViewType) header).HeaderText, () =>
                    {
                        _musicLibrary.SelectedIndex = index;
                        gameObject.SetActive(false);
                    });
                }
            }
        }

        private void SetHeader(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _header.SetActive(false);
            }
            else
            {
                _header.SetActive(true);
                _headerText.text = text;
            }
        }

        private void CreateItem(string body, UnityAction a)
        {
            var btn = Instantiate(_menuItemPrefab, _container);
            btn.Initialize(body, a);
            _navGroup.AddNavigatable(btn.Button);
        }
    }
}