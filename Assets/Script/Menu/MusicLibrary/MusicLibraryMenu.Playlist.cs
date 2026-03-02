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
            }, id++));

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

            // Only allow rename if not Favorites or Current Setlist
            if (SelectedPlaylist != PlaylistContainer.FavoritesPlaylist && !SelectedPlaylist.Ephemeral)
            {
                list.Add(new ButtonViewType(
                    Localize.Key("Menu.MusicLibrary.Popup.Item.RenamePlaylist"),
                    "MusicLibraryIcons[Playlists]", RenamePlaylist)
                );
            }

            // Only allow delete if not Favorites
            if (SelectedPlaylist != PlaylistContainer.FavoritesPlaylist)
            {
                var deleteLabel = SelectedPlaylist == ShowPlaylist
                    ? Localize.Key("Menu.MusicLibrary.Popup.Item.DeleteSetlist")
                    : Localize.Key("Menu.MusicLibrary.Popup.Item.DeletePlaylist");
                list.Add(new ButtonViewType(
                    deleteLabel,
                    "MusicLibraryIcons[Playlists]", DeletePlaylist)
                );
            }

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0)
            {
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

            return list;
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
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"),
                    "MusicLibraryIcons[Back]", LeaveShowMode, BACK_ID),
                new ButtonViewType("Show Setlist", "MusicLibraryIcons[Playlists]", () => { })
            };

            foreach (var song in ShowPlaylist.ToList())
            {
                list.Add(new SongViewType(this, song));
            }

            return list;
        }

        private void SetShowNavigationScheme(bool reset = false)
        {
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

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
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", LeaveShowMode),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.StartShow",
                    OnPlayShowHit),
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

            DialogManager.Instance.ShowSongPickerDialog("Pick Your Poison", this);
        }

        private void LeaveShowMode()
        {
            ShowPlaylist.Clear();

            // Pop the navigation scheme
            Navigator.Instance.PopScheme();
            // We have to reset the navigation scheme so the help bar has the correct yellow button text
            // in the case that we are leaving show mode with a playlist that has entries
            SetNavigationScheme(true);

            // Back to library
            MenuState = MenuState.Library;
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
                if (playlist.Playlist.SongHashes.Count == 0)
                {
                    ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.EmptyPlaylist"));
                    return;
                }

                if (playlist.Playlist.Ephemeral)
                {
                    // No, we won't add the setlist to itself, thanks
                    ToastManager.ToastError(Localize.Key("Menu.MusicLibrary.CannotAddToSelf"));
                    return;
                }

                var i = 0;

                foreach (var song in playlist.Playlist.ToList())
                {
                    ShowPlaylist.AddSong(song);
                    i++;
                }

                if (i > 0)
                {
                    ToastManager.ToastSuccess(Localize.KeyFormat("Menu.MusicLibrary.PlaylistAddedToSet", i));
                }
                else
                {
                    ToastManager.ToastWarning(Localize.Key("Menu.MusicLibrary.NoSongsInPlaylist"));
                }

                if (i > 0 && ShowPlaylist.Count == i)
                {
                    // We need to rebuild the navigation scheme the first time we add song(s)
                    SetNavigationScheme(true);
                }

                // Refresh view if needed
                RefreshAndReselect();

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

        private void OnPlayShowHit()
        {
            if (ShowPlaylist.Count > 0 && PlayerContainer.Players.Count > 0)
            {
                GlobalVariables.State.PlayingAShow = true;
                GlobalVariables.State.ShowSongs = ShowPlaylist.ToList();
                GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs.First();
                GlobalVariables.State.ShowIndex = 0;

                // Make sure we don't come back to play a show after show has been played
                LeaveShowMode();

                MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
            }
        }

        private void MovePlaylistEntryUp()
        {
            if (SelectedPlaylist == null) return;

            if (CurrentSelection is SongViewType selection)
            {
                var song = selection.SongEntry;
                int previousIndex = SelectedIndex;
                SelectedPlaylist.MoveSongUp(song);
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
            if (SelectedPlaylist == null) return;

            if (CurrentSelection is SongViewType selection)
            {
                var song = selection.SongEntry;
                int previousIndex = SelectedIndex;
                SelectedPlaylist.MoveSongDown(song);
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
