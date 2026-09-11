using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Playlists;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public partial class MusicLibraryMenu
    {
        public Playlist       ShowPlaylist   { get; set; }         = new(true);
        private Playlist      _lastPlaylistSelectPlaylist;

        private List<ViewType> CreatePlaylistSelectViewList()
        {
            SongCategory[] emptyCategory = Array.Empty<SongCategory>();
            int id = BACK_ID + 1;
            var list = new List<ViewType>
            {
                new ButtonViewType("YARG", "MusicLibraryIcons[Playlists]", () => { })
            };

            // Add the setlist "playlist" if there are any songs currently in it
            if (ShowPlaylist.Count > 0)
            {
                list.Add(new PlaylistViewType(Localize.Key("Menu.MusicLibrary.CurrentSetlist"), ShowPlaylist,
                    () =>
                    {
                        EnterPlaylistView(ShowPlaylist);
                    }, id));
                id++;
            }

            // Favorites is always on top (within the YARG section)
            list.Add(new PlaylistViewType(
                Localize.Key("Menu.MusicLibrary.Favorites"),
                PlaylistContainer.FavoritesPlaylist,
                () =>
                {
                    EnterPlaylistView(PlaylistContainer.FavoritesPlaylist);
                }, PLAYLIST_ID));

            list.Add(new ButtonViewType(Localize.Key("Menu.MusicLibrary.YourPlaylists"),
                "MusicLibraryIcons[Playlists]", () => { }));

            // Add any other user defined playlists
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                list.Add(new PlaylistViewType(playlist.Name, playlist, () =>
                {
                    EnterPlaylistView(playlist);
                }, id));
                id++;
            }

            // Add "Create New Playlist" button
            list.Add(new ButtonViewType(
                Localize.Key("Menu.MusicLibrary.Popup.Item.CreateNewPlaylist"),
                "MusicLibraryIcons[Playlists]", () =>
            {
                DialogManager.Instance.ShowRenameDialog("New Playlist Name", playlistName =>
                {
                    var playlist = PlaylistContainer.CreatePlaylist(playlistName);
                    ToastManager.ToastSuccess($"Created '{playlistName}'");
                    RefreshAndSelectPlaylist(playlist);
                });
            }, CREATE_NEW_PLAYLIST_ID));

            return list;
        }

        private void ExitPlaylistSelect()
        {
            MenuState = MenuState.Library;
            Refresh();

            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
        }

        private void EnterPlaylistView(Playlist playlist)
        {
            _lastPlaylistSelectPlaylist = playlist;
            SelectedPlaylist = playlist;
            MenuState = MenuState.Playlist;
            Refresh();

            if (!SetIndexTo(i => i is SongViewType))
                SelectedIndex = 0;
        }

        private List<ViewType> CreatePlaylistViewList()
        {
            SetNavigationScheme(true);
            var list = new List<ViewType>{};

            if (SelectedPlaylist.Ephemeral)
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.StartSet"),
                    "MusicLibraryIcons[Playlists]", StartSetlist)
                );
            }
            else
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Popup.Item.AddPlaylistToSetlist"),
                    "MusicLibraryIcons[Playlists]", () => AddPlaylistToSetlist(SelectedPlaylist))
                );
            }

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0)
            {
                AddPlaylistManagementButtons(list);
                return list;
            }

            bool allowdupes = SettingsManager.Settings.AllowDuplicateSongs.Value;
            _totalSongCount = 0;
            _totalStarCount = 0;

            // Add songs in the playlist
            foreach (var section in _sortedSongs)
            {
                foreach (var song in section.Songs)
                {
                    if (allowdupes || !song.IsDuplicate)
                    {
                        var songView = new SongViewType(this, song);
                        list.Add(songView);

                        _totalSongCount++;
                        var starAmount = songView.GetStarAmount();
                        _totalStarCount += starAmount is null ? 0 : StarAmountHelper.GetStarCount(starAmount.Value);
                    }
                }
            }

            AddPlaylistManagementButtons(list);
            return list;
        }

        private void AddPlaylistManagementButtons(List<ViewType> list)
        {
            if (SelectedPlaylist.Ephemeral)
            {
                AddSetlistManagementButtons(list, DeletePlaylist);
                return;
            }

            // Only allow rename if not Favorites or Current Setlist
            if (SelectedPlaylist != PlaylistContainer.FavoritesPlaylist)
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Popup.Item.RenamePlaylist"),
                    "MusicLibraryIcons[Playlists]", RenamePlaylist)
                );
            }

            // Only allow delete if not Favorites
            if (SelectedPlaylist != PlaylistContainer.FavoritesPlaylist)
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Popup.Item.DeletePlaylist"),
                    "MusicLibraryIcons[Playlists]", DeletePlaylist)
                );
            }
        }

        private void AddSetlistManagementButtons(List<ViewType> list, Action deleteAction)
        {
            list.Add(new ButtonViewType(
                Localize.Key("Menu.MusicLibrary.Popup.Item.SaveSetlistToPlaylist"),
                "MusicLibraryIcons[Playlists]", SaveSetlistToPlaylist)
            );
            list.Add(new ButtonViewType(
                Localize.Key("Menu.MusicLibrary.Popup.Item.DeleteSetlist"),
                "MusicLibraryIcons[Playlists]", deleteAction)
            );
        }

        private void SaveSetlistToPlaylist()
        {
            _popupMenu.OpenAddToPlaylist(ShowPlaylist);
        }

        private void RenamePlaylist()
        {
            if (SelectedPlaylist == null) return;

            // Don't allow renaming Favorites
            if (SelectedPlaylist == PlaylistContainer.FavoritesPlaylist)
            {
                ToastManager.ToastError("Cannot rename Favorites playlist");
                return;
            }

            DialogManager.Instance.ShowRenameDialog(SelectedPlaylist.Name, newName =>
            {
                PlaylistContainer.RenamePlaylist(SelectedPlaylist, newName);
                ToastManager.ToastSuccess($"Renamed to '{newName}'");
                RefreshAndReselect();
            });
        }

        private void SortPlaylistAscending()
        {
            if (SelectedPlaylist == null) return;

            SelectedPlaylist.SortByName(ascending: true);
            ToastManager.ToastSuccess("Sorted A-Z");
            RefreshAndReselect();
        }

        private void SortPlaylistDescending()
        {
            if (SelectedPlaylist == null) return;

            SelectedPlaylist.SortByName(ascending: false);
            ToastManager.ToastSuccess("Sorted Z-A");
            RefreshAndReselect();
        }

        private void SortPlaylistByArtistAscending()
        {
            if (SelectedPlaylist == null) return;

            SelectedPlaylist.SortByArtist(ascending: true);
            ToastManager.ToastSuccess("Sorted by Artist A-Z");
            RefreshAndReselect();
        }

        private void SortPlaylistByArtistDescending()
        {
            if (SelectedPlaylist == null) return;

            SelectedPlaylist.SortByArtist(ascending: false);
            ToastManager.ToastSuccess("Sorted by Artist Z-A");
            RefreshAndReselect();
        }

        private void DeletePlaylist()
        {
            if (SelectedPlaylist == null) return;

            // Don't allow deleting Favorites
            if (SelectedPlaylist == PlaylistContainer.FavoritesPlaylist)
            {
                ToastManager.ToastError("Cannot delete this playlist");
                return;
            }

            if (SelectedPlaylist.Ephemeral)
            {
                SelectedPlaylist.Clear();
            }
            else
            {
                PlaylistContainer.DeletePlaylist(SelectedPlaylist);
            }

            ToastManager.ToastSuccess($"Deleted '{SelectedPlaylist.Name}'");

            // Exit back to library
            ExitPlaylistView();
        }

        private List<ViewType> CreateShowViewList()
        {
            _totalSongCount = 0;
            _totalStarCount = 0;

            var list = new List<ViewType>
            {
                new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.StartSet"),
                    "MusicLibraryIcons[Playlists]", StartSetlist)
            };

            foreach (var song in ShowPlaylist.ToList())
            {
                var songView = new SongViewType(this, song);
                list.Add(songView);

                _totalSongCount++;
                var starAmount = songView.GetStarAmount();
                _totalStarCount += starAmount is null ? 0 : StarAmountHelper.GetStarCount(starAmount.Value);
            }

            AddSetlistManagementButtons(list, DeleteShowSetlist);

            return list;
        }

        private void SetShowNavigationScheme(bool reset = false)
        {
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
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
                new NavigationScheme.Entry(MenuAction.Left, "Menu.MusicLibrary.MoveInPlaylist",
                    MovePlaylistEntryUp),
                new NavigationScheme.Entry(MenuAction.Right, "Menu.MusicLibrary.MoveInPlaylist",
                    MovePlaylistEntryDown),
                new NavigationScheme.Entry(
                    MenuAction.Green,
                    "Menu.MusicLibrary.AddHoldStartSet",
                    OnGreenTap,
                    holdSeconds: GREEN_HOLD_SECONDS,
                    onHoldHandler: OnGreenHold,
                    hide: true),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", LeaveShowMode, hide: true),
                new NavigationScheme.Entry(
                    MenuAction.Yellow,
                    "Menu.MusicLibrary.HoldPlayShow",
                    () => { },
                    holdSeconds: GREEN_HOLD_SECONDS,
                    onHoldHandler: OpenShowPicker),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Filters", OpenFilters),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.MusicLibrary.MoreOptions",
                    OnOrangeHit, OnOrangeRelease),
            }, false));
        }

        private void ExitPlaylistView()
        {
            var lastPlaylist = _lastPlaylistSelectPlaylist;
            SelectedPlaylist = null;
            MenuState = MenuState.PlaylistSelect;
            SetNavigationScheme(true);
            ClearPreview();
            // Prevent an out-of-range song index from rendering an empty list while we rebuild.
            SelectedIndex = 0;
            Refresh();

            if (!SetIndexTo(i => i is PlaylistViewType pv && pv.Playlist == lastPlaylist))
            {
                // Select playlist button
                SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
            }
            _sidebar.UpdateSidebar(true);
        }

        private void EnterShowMode()
        {
            // Save the current selected index if we're in the main library
            if (MenuState == MenuState.Library)
            {
                _mainLibraryIndex = SelectedIndex;
            }

            // Update the navigation scheme
            SetShowNavigationScheme();

            // Display the show screen
            MenuState = MenuState.Show;
            Refresh();

            if (!SetIndexTo(i => i is SongViewType))
                SelectedIndex = 0;

            OpenShowPicker();
        }

        private void OpenShowPicker()
        {
            SelectedIndex = 0;
            DialogManager.Instance.ShowSongPickerDialog("Pick Your Poison", this);
        }

        private void LeaveShowMode()
        {
            // Pop the navigation scheme
            Navigator.Instance.PopScheme();
            // We have to reset the navigation scheme so the help bar has the correct yellow button text
            // in the case that we are leaving show mode with a playlist that has entries
            SetNavigationScheme(true);

            // Back to library
            MenuState = MenuState.Library;
            // Show mode can be entered from a saved playlist. Do not carry that playlist's
            // filtering context back into the main library.
            SelectedPlaylist = null;
            Refresh();

            // Restore the main library index if it is valid
            if (_mainLibraryIndex != -1)
            {
                SelectedIndex = _mainLibraryIndex;
            }
            else
            {
                SetIndexTo(i => i is ButtonViewType { ID: RANDOM_SONG_ID });
            }
        }

        private void DeleteShowSetlist()
        {
            string setlistName = ShowPlaylist.Name;
            ShowPlaylist.Clear();
            LeaveShowMode();
            ToastManager.ToastSuccess($"Deleted '{setlistName}'");
        }

        private void StartSetlist()
        {
            if (ShowPlaylist.Count > 0 && PlayerContainer.Players.Count > 0)
            {
                // If we are in the main library, save the current index
                if (MenuState == MenuState.Library)
                {
                    _mainLibraryIndex = SelectedIndex;
                }

                GlobalVariables.State.PlayingAShow = true;
                GlobalVariables.State.ShowSongs = ShowPlaylist.ToList();
                GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs.First();
                GlobalVariables.State.ShowIndex = 0;
                MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
            }
        }

        private void AddToPlaylist()
        {
            if (CurrentSelection is PlaylistViewType playlist)
            {
                AddPlaylistToSetlist(playlist.Playlist);
                return;
            }

            if (CurrentSelection is SongViewType selection)
            {
                ShowPlaylist.AddSong(selection.SongEntry);
                if (ShowPlaylist.Count == 1)
                {
                    // We need to rebuild the navigation scheme after adding the first song
                    SetNavigationScheme(true);
                }

                ToastManager.ToastSuccess(Localize.Key("Menu.MusicLibrary.AddedToSet"));
            }
        }

        public void AddPlaylistToSetlist(Playlist playlist)
        {
            if (playlist.SongHashes.Count == 0)
            {
                ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.EmptyPlaylist"));
                return;
            }

            if (playlist.Ephemeral)
            {
                // No, we won't add the setlist to itself, thanks
                ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.CannotAddToSelf"));
                return;
            }

            var count = 0;

            foreach (var song in playlist.ToList())
            {
                ShowPlaylist.AddSong(song);
                count++;
            }

            if (count > 0)
            {
                ToastManager.ToastSuccess(Localize.KeyFormat("Menu.MusicLibrary.PlaylistAddedToSet", count));
            }
            else
            {
                ToastManager.ToastWarning(Localize.Key("Menu.MusicLibrary.NoSongsInPlaylist"));
            }

            if (count > 0 && ShowPlaylist.Count == count)
            {
                // We need to rebuild the navigation scheme the first time we add song(s)
                SetNavigationScheme(true);
            }

            RefreshAndReselect();
        }

        private void MovePlaylistEntryUp()
        {
            var playlist = MenuState == MenuState.Show ? ShowPlaylist : SelectedPlaylist;
            if (playlist == null) return;

            if (CurrentSelection is SongViewType selection)
            {
                var song = selection.SongEntry;
                int previousIndex = SelectedIndex;
                playlist.MoveSongUp(song);
                Refresh();
                if (!SetIndexTo(i => i is SongViewType view && view.SongEntry == song))
                {
                    SelectedIndex = previousIndex < 0 ? 0 :
                        previousIndex >= ViewList.Count ? ViewList.Count - 1 : previousIndex;
                }
            }
        }

        private void MovePlaylistEntryDown()
        {
            var playlist = MenuState == MenuState.Show ? ShowPlaylist : SelectedPlaylist;
            if (playlist == null) return;

            if (CurrentSelection is SongViewType selection)
            {
                var song = selection.SongEntry;
                int previousIndex = SelectedIndex;
                playlist.MoveSongDown(song);
                Refresh();
                if (!SetIndexTo(i => i is SongViewType view && view.SongEntry == song))
                {
                    SelectedIndex = previousIndex < 0 ? 0 :
                        previousIndex >= ViewList.Count ? ViewList.Count - 1 : previousIndex;
                }
            }
        }
    }
}
