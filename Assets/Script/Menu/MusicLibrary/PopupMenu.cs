using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Playlists;
using YARG.Settings;

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

            // Only show these options if we are selecting a song
            if (_musicLibrary.CurrentSelection is SongViewType songViewType)
            {
                var song = songViewType.SongEntry;

                // Add/remove to liked songs
                if (!PlaylistContainer.LikedSongsPlaylist.ContainsSong(song))
                {
                    CreateItem("Add To Liked Songs", () =>
                    {
                        PlaylistContainer.LikedSongsPlaylist.AddSong(songViewType.SongEntry);

                        gameObject.SetActive(false);
                    });
                }
                else
                {
                    CreateItem("Remove From Liked Songs", () =>
                    {
                        PlaylistContainer.LikedSongsPlaylist.RemoveSong(songViewType.SongEntry);

                        // If we are in the liked songs menu, then update the playlist
                        // to remove the song that was just removed.
                        if (MusicLibraryMenu.SelectedPlaylist == PlaylistContainer.LikedSongsPlaylist)
                        {
                            _musicLibrary.RefreshAndReselect();
                        }

                        gameObject.SetActive(false);
                    });
                }

                // Everything here is an advanced setting
                if (SettingsManager.Settings.ShowAdvancedMusicLibraryOptions.Value)
                {
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
        }

        private void CreateSortSelect()
        {
            SetHeader("Sort By...");

            foreach (var sort in EnumExtensions<SongAttribute>.Values)
            {
                // Skip theses because they don't make sense
                if (sort == SongAttribute.Unspecified) continue;
                if (sort == SongAttribute.Instrument) continue;

                // Create an item for it
                CreateItem(sort.ToLocalizedName(), () =>
                {
                    _musicLibrary.ChangeSort(sort);
                    gameObject.SetActive(false);
                });
            }
        }

        private void CreateGoToSection()
        {
            SetHeader("Go To...");

            foreach (var (header, index) in _musicLibrary.GetSections())
            {
                CreateItem(((SortHeaderViewType) header).HeaderText, () =>
                {
                    _musicLibrary.SelectedIndex = index;
                    gameObject.SetActive(false);
                });
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