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
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () =>
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

            CreateItem("RandomSong", () =>
            {
                _musicLibrary.SelectRandomSong();
                gameObject.SetActive(false);
            });

            CreateItem("BackToTop", () =>
            {
                _musicLibrary.SelectedIndex = 0;
                gameObject.SetActive(false);
            });

            if (_musicLibrary.HasSortHeaders)
            {
                CreateItem("SortBy", SettingsManager.Settings.LibrarySort.ToLocalizedName(), () =>
                {
                    _menuState = State.SortSelect;
                    UpdateForState();
                });
            }
            
            CreateItem("GoToSection", () =>
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
                    CreateItem("AddToFavorites", () =>
                    {
                        viewType.FavoriteClick();
                        _musicLibrary.RefreshViewsObjects();

                        gameObject.SetActive(false);
                    });
                }
                else
                {
                    CreateItem("RemoveFromFavorites", () =>
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

                CreateItem("ViewSongFolder", () =>
                {
                    FileExplorerHelper.OpenFolder(song.Directory);

                    gameObject.SetActive(false);
                });

                CreateItem("CopySongChecksum", () =>
                {
                    GUIUtility.systemCopyBuffer = song.Hash.ToString();

                    gameObject.SetActive(false);
                });
            }
        }

        private void CreateSortSelect()
        {
            SetLocalizedHeader("SortBy");

            foreach (var sort in EnumExtensions<SortAttribute>.Values)
            {
                // Skip theses because they don't make sense
                if (sort == SortAttribute.Unspecified)
                {
                    continue;
                }

                if (sort >= SortAttribute.Instrument)
                {
                    break;
                }

                CreateItemUnlocalized(sort.ToLocalizedName(), () =>
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
                    CreateItemUnlocalized(attribute.ToLocalizedName(), () =>
                    {
                        _musicLibrary.ChangeSort(attribute);
                        gameObject.SetActive(false);
                    });
                }
            }
        }

        private void CreateGoToSection()
        {
            SetLocalizedHeader("GoTo");

            foreach (var (name, index) in _musicLibrary.Shortcuts)
            {
                CreateItemUnlocalized(name, () =>
                {
                    _musicLibrary.SelectedIndex = index;
                    gameObject.SetActive(false);
                });
            }
        }

        private void SetLocalizedHeader(string localizeKey)
        {
            SetHeader(Localize.Key("Menu.MusicLibrary.Popup.Header", localizeKey));
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

        private void CreateItem(string localizeKey, UnityAction a)
        {
            var localized = Localize.Key("Menu.MusicLibrary.Popup.Item", localizeKey);
            CreateItemUnlocalized(localized, a);
        }

        private void CreateItem(string localizeKey, string formatArg, UnityAction a)
        {
            var localized = Localize.KeyFormat(("Menu.MusicLibrary.Popup.Item", localizeKey), formatArg);
            CreateItemUnlocalized(localized, a);
        }

        private void CreateItemUnlocalized(string body, UnityAction a)
        {
            var btn = Instantiate(_menuItemPrefab, _container);
            btn.Initialize(body, a);
            _navGroup.AddNavigatable(btn.Button);
        }
    }
}