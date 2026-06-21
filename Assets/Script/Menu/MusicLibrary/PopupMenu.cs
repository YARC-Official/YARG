using System.Linq;
using Cysharp.Threading.Tasks;
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
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Playlists;
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
            GoToSection,
            AddToPlaylist,
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
            _musicLibrary.RefreshNavigationSchemeAfterPopup();
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
                case State.AddToPlaylist:
                    CreateAddToPlayList();
                    break;
            }

            _navGroup.SelectFirst();
        }

        private void CreateMainMenu()
        {
            SetHeader(null);

            if (_musicLibrary.MenuState != MenuState.PlaylistSelect)
            {
                CreateItem("RandomSong", () =>
                {
                    _musicLibrary.SelectRandomSong();
                    gameObject.SetActive(false);
                });
            }

            CreateItem("BackToTop", () =>
            {
                _musicLibrary.SelectedIndex = 0;
                gameObject.SetActive(false);
            });

            CreateItem("ScanSongs", () =>
            {
                _musicLibrary.RefreshSongs();
                gameObject.SetActive(false);
            });

            if (_musicLibrary.MenuState != MenuState.PlaylistSelect)
            {
                CreateItem("SortBy", _musicLibrary.GetPopupSortLabel(), () =>
                {
                    _menuState = State.SortSelect;
                    UpdateForState();
                });
            }

            if (_musicLibrary.MenuState != MenuState.Playlist &&
                _musicLibrary.MenuState != MenuState.PlaylistSelect &&
                _musicLibrary.HasSortHeaders)
            {
                CreateItem("GoToSection", () =>
                {
                    _menuState = State.GoToSection;
                    UpdateForState();
                });
            }

            if (_musicLibrary.MenuState == MenuState.Library && !_musicLibrary.PlaylistMode)
            {
                bool hasCollapsed = false;
                bool hasExpanded = false;
                foreach (var section in _musicLibrary.SortedSongs)
                {
                    if (section.Collapsed)
                    {
                        hasCollapsed = true;
                    }
                    else
                    {
                        hasExpanded = true;
                    }

                    if (hasCollapsed && hasExpanded) break;
                }

                if (hasCollapsed)
                {
                    CreateItem("ExpandAll", () =>
                    {
                        _musicLibrary.ExpandAll();
                        gameObject.SetActive(false);
                    });
                }

                if (hasExpanded)
                {
                    CreateItem("CollapseAll", () =>
                    {
                        _musicLibrary.CollapseAll();
                        gameObject.SetActive(false);
                    });
                }
            }

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

                if (viewType is SongViewType)
                {
                    CreateItemUnlocalized(_musicLibrary.GetGreenHoldActionLabel(), () =>
                    {
                        _musicLibrary.ExecuteGreenHoldAction();
                        gameObject.SetActive(false);
                    });

                    bool isInPlaylist = _musicLibrary.MenuState == MenuState.Playlist &&
                        _musicLibrary.SelectedPlaylist != null &&
                        _musicLibrary.SelectedPlaylist != PlaylistContainer.FavoritesPlaylist;
                    bool isSetlist = isInPlaylist && _musicLibrary.SelectedPlaylist.Ephemeral;

                    void AddRemoveFromPlaylistItem()
                    {
                        var removeKey = _musicLibrary.SelectedPlaylist.Ephemeral
                            ? "RemoveFromSetlist"
                            : "RemoveFromPlaylist";
                        CreateItem(removeKey, () =>
                        {
                            var songView = viewType as SongViewType;
                            _musicLibrary.SelectedPlaylist.RemoveSong(songView.SongEntry);
                            _musicLibrary.RefreshAndReselect();
                            gameObject.SetActive(false);
                            ToastManager.ToastSuccess("Removed from playlist");
                        });
                    }

                    if (isSetlist)
                        AddRemoveFromPlaylistItem();

                    // Show "Add to Playlist" even when editing a playlist.
                    CreateItem("AddToPlaylist", () =>
                    {
                        _menuState = State.AddToPlaylist;
                        UpdateForState();
                    });

                    // Show "Remove from Playlist" if we're editing a playlist
                    if (isInPlaylist && !isSetlist)
                        AddRemoveFromPlaylistItem();
                }
            }

            if (viewType is PlaylistViewType playlistView &&
                playlistView.Playlist != PlaylistContainer.FavoritesPlaylist)
            {
                if (!playlistView.Playlist.Ephemeral)
                {
                    CreateItem("RenamePlaylist", () =>
                    {
                        DialogManager.Instance.ShowRenameDialog(playlistView.Playlist.Name, newName =>
                        {
                            PlaylistContainer.RenamePlaylist(playlistView.Playlist, newName);
                            ToastManager.ToastSuccess($"Renamed to '{newName}'");
                            _musicLibrary.RefreshAndSelectPlaylist(playlistView.Playlist);
                        });

                        CloseAfterDialog().Forget();
                    });
                }

                var deleteLabel = playlistView.Playlist.Ephemeral
                    ? Localize.Key("Menu.MusicLibrary.Popup.Item.DeleteSetlist")
                    : Localize.Key("Menu.MusicLibrary.Popup.Item.DeletePlaylist");
                CreateItemUnlocalized(deleteLabel, () =>
                {
                    // Special handling for the ad hoc setlist
                    if (playlistView.Playlist.Ephemeral)
                    {
                        playlistView.Playlist.Clear();
                    }
                    else
                    {
                        PlaylistContainer.DeletePlaylist(playlistView.Playlist);
                    }
                    
                    ToastManager.ToastSuccess($"Deleted '{playlistView.Playlist.Name}'");

                    _musicLibrary.RefreshAndReselect();
                    gameObject.SetActive(false);
                    // Annoyingly, this has to be done after the popup menu is made inactive, requring duplicate if statements
                    if (playlistView.Playlist.Ephemeral)
                    {
                        _musicLibrary.SetNavigationScheme(true);
                    }
                });
            }

            // Only show these options if we are selecting a song
            if (viewType is SongViewType songViewType &&
                SettingsManager.Settings.ShowAdvancedMusicLibraryOptions.Value)
            {
                var song = songViewType.SongEntry;

                CreateItem("ViewSongFolder", () =>
                {
                    switch (song.SubType)
                    {
                        case EntryType.Ini:
                        case EntryType.ExCON:
                            FileExplorerHelper.OpenFolder(song.ActualLocation);
                            break;
                        case EntryType.Sng:
                        case EntryType.CON:
                            FileExplorerHelper.OpenToFile(song.ActualLocation);
                            break;
                    }
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

            if (_musicLibrary.MenuState == MenuState.Playlist)
            {
                CreateItemUnlocalized($"{SortAttribute.Name.ToLocalizedName()} (A-Z)", () =>
                {
                    _musicLibrary.ApplySortFromPopup(SortAttribute.Name, ascending: true);
                    gameObject.SetActive(false);
                });

                CreateItemUnlocalized($"{SortAttribute.Name.ToLocalizedName()} (Z-A)", () =>
                {
                    _musicLibrary.ApplySortFromPopup(SortAttribute.Name, ascending: false);
                    gameObject.SetActive(false);
                });

                CreateItemUnlocalized($"{SortAttribute.Artist.ToLocalizedName()} (A-Z)", () =>
                {
                    _musicLibrary.ApplySortFromPopup(SortAttribute.Artist, ascending: true);
                    gameObject.SetActive(false);
                });

                CreateItemUnlocalized($"{SortAttribute.Artist.ToLocalizedName()} (Z-A)", () =>
                {
                    _musicLibrary.ApplySortFromPopup(SortAttribute.Artist, ascending: false);
                    gameObject.SetActive(false);
                });

                return;
            }

            foreach (var sort in EnumExtensions<SortAttribute>.Values)
            {
                // Skip theses because they don't make sense
                if (sort == SortAttribute.Unspecified)
                {
                    continue;
                }

                // Skip Play count if there are no real players
                if (sort == SortAttribute.Playcount && PlayerContainer.OnlyHasBotsActive())
                {
                    continue;
                }

                if (sort >= SortAttribute.Instrument)
                {
                    break;
                }

                CreateItemUnlocalized(sort.ToLocalizedName(), () =>
                {
                    _musicLibrary.ApplySortFromPopup(sort);
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

        private void CreateAddToPlayList()
        {
            // Get the list of playlists from PlaylistContainer and create items for each
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                if (_musicLibrary.MenuState == MenuState.Playlist &&
                    _musicLibrary.SelectedPlaylist != null &&
                    ReferenceEquals(playlist, _musicLibrary.SelectedPlaylist))
                {
                    continue;
                }

                CreateItemUnlocalized(playlist.Name, () =>
                {
                    if (_musicLibrary.CurrentSelection is SongViewType songView)
                    {
                        var song = songView.SongEntry;
                        var artist = song.Artist;
                        var title = song.Name;
                        // Add the song to the playlist
                        _musicLibrary.CurrentSelection.AddToPlaylist(playlist);
                        gameObject.SetActive(false);
                        ToastManager.ToastSuccess($"Added {artist} - {title} to {playlist.Name}");
                    }
                    else if (_musicLibrary.CurrentSelection is PlaylistViewType playlistView)
                    {
                        var songs = playlistView.Playlist.ToList();
                        foreach (var song in songs)
                        {
                            playlist.AddSong(song);
                        }

                        gameObject.SetActive(false);
                        ToastManager.ToastSuccess($"Added {songs.Count} songs to {playlist.Name}");
                        _musicLibrary.RefreshAndReselect();
                    }
                });
            }

            // Add option to create new playlist
            CreateItem("CreateNewPlaylist", () =>
            {

                // TODO: Localize all these strings

                // Show text entry dialog
                DialogManager.Instance.ShowRenameDialog("New Playlist Name", value =>
                {
                    // Make sure we aren't being Jadened
                    if (value == Localize.Key("Menu.MusicLibrary.CurrentSetlist"))
                    {
                        ToastManager.ToastError("You can't create a playlist with that name");
                        CloseAfterDialog().Forget();
                        return;
                    }

                    // Create the playlist
                    var playlist = PlaylistContainer.CreatePlaylist(value);
                    // Add selected song to new playlist
                    if (_musicLibrary.CurrentSelection is SongViewType songView)
                    {
                        songView.AddToPlaylist(playlist);
                    }
                    else if (_musicLibrary.CurrentSelection is PlaylistViewType playlistView)
                    {
                        foreach(var song in playlistView.Playlist.ToList())
                        {
                            playlist.AddSong(song);
                        }
                    }
                    else
                    {
                        ToastManager.ToastError("You can't add that to a playlist");
                        PlaylistContainer.DeletePlaylist(playlist);
                        CloseAfterDialog().Forget();
                        return;
                    }

                    // Close the popup after the rename dialog is fully closed
                    CloseAfterDialog().Forget();
                    _musicLibrary.RefreshAndReselect();
                    ToastManager.ToastSuccess("Playlist Created");
                });
            });
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

        private async UniTaskVoid CloseAfterDialog()
        {
            await DialogManager.Instance.WaitUntilCurrentClosed();
            if (this == null) return;
            gameObject.SetActive(false);
            _musicLibrary.SetNavigationScheme(true);
        }

        private void CreateItemUnlocalized(string body, UnityAction a)
        {
            var btn = Instantiate(_menuItemPrefab, _container);
            btn.Initialize(body, a);
            _navGroup.AddNavigatable(btn.Button);
        }
    }
}
