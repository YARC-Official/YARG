using System;
using System.Collections.Generic;
using System.Linq;
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
        private YARG.Multiplayer.MultiplayerShowPlaylist _multiplayerShowPlaylist;

        public Playlist ShowPlaylist
        {
            get
            {
                // In multiplayer, use the networked playlist
                if (_multiplayerShowPlaylist != null)
                {
                    return _multiplayerShowPlaylist.ShowPlaylist;
                }
                // Fallback to local playlist
                return _localShowPlaylist;
            }
            set
            {
                if (_multiplayerShowPlaylist != null)
                {
                    _multiplayerShowPlaylist.ShowPlaylist = value;
                }
                else
                {
                    _localShowPlaylist = value;
                }
            }
        }

        private Playlist _localShowPlaylist = new(true);

        private void OnMultiplayerPlaylistUpdated()
        {
            UnityEngine.Debug.Log($"[MusicLibraryMenu] OnPlaylistUpdated triggered - refreshing UI. Playlist count: {ShowPlaylist.Count}");
            // Always refresh the view list when playlist changes
            Refresh();
        }

        private void EnsureMultiplayerShowPlaylist()
        {
            if (_multiplayerShowPlaylist == null && Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                // Get the spawned MultiplayerShowPlaylist from the NetworkManager
                _multiplayerShowPlaylist = Networking.YargNetworkManager.Instance.MultiplayerShowPlaylist;
                
                if (_multiplayerShowPlaylist != null)
                {
                    // Subscribe to playlist updates
                    _multiplayerShowPlaylist.OnPlaylistUpdated -= OnMultiplayerPlaylistUpdated;
                    _multiplayerShowPlaylist.OnPlaylistUpdated += OnMultiplayerPlaylistUpdated;
                    
                    // Wait for initial sync on clients
                    if (!Networking.YargNetworkManager.Instance.IsHosting && !_multiplayerShowPlaylist.HasReceivedInitialSync)
                    {
                        UnityEngine.Debug.Log($"[MusicLibraryMenu] Client waiting for initial playlist sync...");
                        StartCoroutine(WaitForInitialSync());
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[MusicLibraryMenu] MultiplayerShowPlaylist reference obtained. Current count: {_multiplayerShowPlaylist.ShowPlaylist.Count}");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[MusicLibraryMenu] MultiplayerShowPlaylist not found - may not be spawned yet");
                }
            }
            else if (_multiplayerShowPlaylist != null)
            {
                // Re-subscribe if we already have the reference (in case we navigated away and back)
                _multiplayerShowPlaylist.OnPlaylistUpdated -= OnMultiplayerPlaylistUpdated;
                _multiplayerShowPlaylist.OnPlaylistUpdated += OnMultiplayerPlaylistUpdated;
                UnityEngine.Debug.Log($"[MusicLibraryMenu] Re-subscribed to playlist updates. Current count: {ShowPlaylist.Count}");
            }
        }

        private System.Collections.IEnumerator WaitForInitialSync()
        {
            float timeout = 2f;
            float elapsed = 0f;
            
            while (!_multiplayerShowPlaylist.HasReceivedInitialSync && elapsed < timeout)
            {
                yield return new UnityEngine.WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (_multiplayerShowPlaylist.HasReceivedInitialSync)
            {
                UnityEngine.Debug.Log($"[MusicLibraryMenu] Initial sync received! Playlist count: {_multiplayerShowPlaylist.ShowPlaylist.Count}");
                Refresh();
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[MusicLibraryMenu] Timed out waiting for initial sync. Proceeding anyway.");
            }
        }

        // Helper methods for multiplayer show playlist management
        public void AddSongToMultiplayerShow(string songHash)
        {
            EnsureMultiplayerShowPlaylist();
            if (_multiplayerShowPlaylist != null)
            {
                // Check if song is already in the playlist
                if (_multiplayerShowPlaylist.IsInPlaylist(songHash))
                {
                    UnityEngine.Debug.Log($"[MusicLibraryMenu] Song {songHash} already in playlist, skipping add");
                    return;
                }
                
                // Get the player's name
                string playerName = "Unknown";
                if (PlayerContainer.Players.Count > 0)
                {
                    playerName = PlayerContainer.Players[0].Profile.Name;
                }
                
                UnityEngine.Debug.Log($"[MusicLibraryMenu] Calling CmdAddSongToShow for hash: {songHash} from player: {playerName}");
                _multiplayerShowPlaylist.CmdAddSongToShow(songHash, playerName);
            }
            else
            {
                UnityEngine.Debug.LogError("[MusicLibraryMenu] Cannot add song - MultiplayerShowPlaylist is null!");
            }
        }

        public void RemoveSongFromMultiplayerShow(string songHash)
        {
            EnsureMultiplayerShowPlaylist();
            if (_multiplayerShowPlaylist != null)
            {
                // Get the player's name
                string playerName = "Unknown";
                if (PlayerContainer.Players.Count > 0)
                {
                    playerName = PlayerContainer.Players[0].Profile.Name;
                }
                
                _multiplayerShowPlaylist.CmdRemoveSongFromShow(songHash, playerName);
            }
        }

        public void StartMultiplayerShow()
        {
            EnsureMultiplayerShowPlaylist();
            if (_multiplayerShowPlaylist != null && Networking.YargNetworkManager.Instance.IsHosting)
            {
                _multiplayerShowPlaylist.CmdStartShow();
            }
        }

        private List<ViewType> CreatePlaylistSelectViewList()
        {
            SongCategory[] emptyCategory = Array.Empty<SongCategory>();
            int id = BACK_ID + 1;
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"),
                    "MusicLibraryIcons[Back]", () =>
                    {
                        SelectedPlaylist = null;
                        MenuState = MenuState.Library;
                        Refresh();
                    }, BACK_ID)
            };

            list.Add(new ButtonViewType("YARG", "MusicLibraryIcons[Playlists]", () => { }));

            // Favorites is always on top
            list.Add(new PlaylistViewType(
                Localize.Key("Menu.MusicLibrary.Favorites"),
                PlaylistContainer.FavoritesPlaylist,
                () =>
                {
                    SelectedPlaylist = PlaylistContainer.FavoritesPlaylist;
                    MenuState = MenuState.Playlist;
                    Refresh();
                }, PLAYLIST_ID));

            list.Add(new ButtonViewType(Localize.Key("Menu.MusicLibrary.YourPlaylists"),
                "MusicLibraryIcons[Playlists]", () => { }));

            // Add the setlist "playlist" if there are any songs currently in it
            if (ShowPlaylist.Count > 0)
            {
                list.Add(new PlaylistViewType(Localize.Key("Menu.MusicLibrary.CurrentSetlist"), ShowPlaylist,
                    () =>
                    {
                        SelectedPlaylist = ShowPlaylist;
                        MenuState = MenuState.Playlist;
                        Refresh();
                    }, id));
                id++;
            }

            // Add any other user defined playlists
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                list.Add(new PlaylistViewType(playlist.Name, playlist, () =>
                {
                    SelectedPlaylist = playlist;
                    MenuState = MenuState.Playlist;
                    Refresh();
                }, id));
                id++;
            }

            return list;
        }

        private List<ViewType> CreatePlaylistViewList()
        {
            SetNavigationScheme(true);
            var list = new List<ViewType>
            {
                new ButtonViewType(Localize.Key("Menu.MusicLibrary.Back"),
                    "MusicLibraryIcons[Back]", ExitPlaylistView, BACK_ID)
            };

            // If `_sortedSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_sortedSongs is null || SongContainer.Count <= 0 ||
                !_sortedSongs.Any(section => section.Songs.Length > 0))
            {
                return list;
            }

            bool allowdupes = SettingsManager.Settings.AllowDuplicateSongs.Value;
            foreach (var section in _sortedSongs)
            {
                list.Add(new SortHeaderViewType(
                    section.Category.ToUpperInvariant(),
                    section.Songs.Length,
                    section.CategoryGroup));

                foreach (var song in section.Songs)
                {
                    if (allowdupes || !song.IsDuplicate)
                    {
                        list.Add(new SongViewType(this, song));
                    }
                }
            }

            CalculateCategoryHeaderIndices(list);
            return list;
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
                    OnButtonHit, OnButtonRelease),
            }, false));
        }

        private void ExitPlaylistView()
        {
            SelectedPlaylist = null;
            MenuState = MenuState.PlaylistSelect;
            SetNavigationScheme(true);
            Refresh();

            // Select playlist button
            // TODO: Fix this to select the playlist we entered from, not favorites
            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
        }

        private void ExitPlaylistSelect()
        {
            MenuState = MenuState.Library;
            Refresh();

            SetIndexTo(i => i is ButtonViewType { ID: PLAYLIST_ID });
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
			SelectedPlaylist = ShowPlaylist;
            MenuState = MenuState.Show;
            Refresh();

            DialogManager.Instance.ShowSongPickerDialog("Pick Your Poison", this);
        }

        private void LeaveShowMode()
        {
            SelectedPlaylist = null;
            // Don't clear the setlist - users may want to go back to library to add more songs
            // ShowPlaylist.Clear();

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
            if (ShowPlaylist.Count == 0)
            {
                ToastManager.ToastError("Add songs to the setlist first!");
                return;
            }
            
            if (PlayerContainer.Players.Count == 0)
            {
                ToastManager.ToastError("No players available!");
                return;
            }
            
            bool isMultiplayer = Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive;
            
            if (isMultiplayer)
            {
                // In multiplayer, only host can start
                if (Networking.YargNetworkManager.Instance.IsHosting)
                {
                    UnityEngine.Debug.Log($"[MusicLibraryMenu] Host starting show with {ShowPlaylist.Count} songs from MusicLibrary");
                    ToastManager.ToastInformation($"Starting show with {ShowPlaylist.Count} songs!");
                    StartMultiplayerShow();
                }
                else
                {
                    ToastManager.ToastWarning("Only the host can start the show");
                }
                return;
            }
            
            // Single player mode
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

        private void AddToPlaylist()
        {
            bool isMultiplayer = Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive;
            
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
                    if (isMultiplayer)
                    {
                        AddSongToMultiplayerShow(song.Hash.ToString());
                    }
                    else
                    {
                        ShowPlaylist.AddSong(song);
                    }
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

                // If we are in the playlist view, we need to refresh the view
                if (MenuState == MenuState.PlaylistSelect)
                {
                    RefreshAndReselect();
                }

                return;
            }

            if (CurrentSelection is SongViewType selection)
            {
                if (isMultiplayer)
                {
                    AddSongToMultiplayerShow(selection.SongEntry.Hash.ToString());
                }
                else
                {
                    ShowPlaylist.AddSong(selection.SongEntry);
                }
                
                if (ShowPlaylist.Count == 1)
                {
                    // We need to rebuild the navigation scheme after adding the first song
                    SetNavigationScheme(true);
                }

                ToastManager.ToastSuccess(Localize.Key("Menu.MusicLibrary.AddedToSet"));
            }
        }

        private void QuickStartShow()
        {
            // Quick start for multiplayer - starts the show immediately if there are songs
            bool isMultiplayer = Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive;
            
            if (!isMultiplayer)
            {
                // Fallback to regular add to playlist behavior in single player
                AddToPlaylist();
                return;
            }
            
            if (ShowPlaylist.Count == 0)
            {
                ToastManager.ToastWarning("Add songs to the setlist first!");
                return;
            }
            
            if (PlayerContainer.Players.Count == 0)
            {
                ToastManager.ToastError("No players available!");
                return;
            }
            
            // Only host can start the show
            if (!Networking.YargNetworkManager.Instance.IsHosting)
            {
                ToastManager.ToastWarning("Only the host can start the show");
                return;
            }
            
            UnityEngine.Debug.Log($"[MusicLibraryMenu] Quick starting show with {ShowPlaylist.Count} songs");
            ToastManager.ToastSuccess($"Starting show with {ShowPlaylist.Count} songs!");
            StartMultiplayerShow();
        }

        private void OnPlayShowHit()
        {
            if (ShowPlaylist.Count > 0 && PlayerContainer.Players.Count > 0)
            {
                bool isMultiplayer = Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive;
                
                if (isMultiplayer)
                {
                    // In multiplayer, only host can start
                    if (Networking.YargNetworkManager.Instance.IsHosting)
                    {
                        StartMultiplayerShow();
                    }
                    else
                    {
                        ToastManager.ToastWarning("Only the host can start the show");
                    }
                    return;
                }
                
                // Single player mode
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
            if (CurrentSelection is SongViewType selection)
            {
                SelectedPlaylist.MoveSongUp(selection.SongEntry);
                Refresh();
            }
        }

        private void MovePlaylistEntryDown()
        {
            if (CurrentSelection is SongViewType selection)
            {
                SelectedPlaylist.MoveSongDown(selection.SongEntry);
                Refresh();
            }
        }
    }
}