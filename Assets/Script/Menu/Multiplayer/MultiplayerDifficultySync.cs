using System.Collections.Generic;
using Mirror;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Networking;
using YARG.Player;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Handles multiplayer synchronization for difficulty selection.
    /// Syncs player selections and handles ready state.
    /// </summary>
    public class MultiplayerDifficultySync : MonoBehaviour
    {
        private bool _isMultiplayer;
        private bool _isHost;
        private readonly HashSet<int> _syncedProfileIndices = new();
        private YargNetworkManager _subscribedNetworkManager;

        /// <summary>
        /// Event fired when host is waiting for other players.
        /// Parameter is the waiting message to display.
        /// </summary>
        public System.Action<string> OnWaitingForPlayers;

        private void OnEnable()
        {
            SubscribeToNetworkEvents();
            RefreshNetworkState();
        }

        private void OnDisable()
        {
            UnsubscribeFromNetworkEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        private void SubscribeToNetworkEvents()
        {
            var manager = YargNetworkManager.Instance;
            if (manager == null || _subscribedNetworkManager == manager)
            {
                return;
            }

            UnsubscribeFromNetworkEvents();

            manager.OnLobbyJoined += HandleLobbyJoined;
            manager.OnLobbyLeft += HandleLobbyLeft;
            _subscribedNetworkManager = manager;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_subscribedNetworkManager == null)
            {
                return;
            }

            _subscribedNetworkManager.OnLobbyJoined -= HandleLobbyJoined;
            _subscribedNetworkManager.OnLobbyLeft -= HandleLobbyLeft;
            _subscribedNetworkManager = null;
        }

        private void HandleLobbyJoined(YargNetworkManager.LobbyInfo _)
        {
            RefreshNetworkState();
        }

        private void HandleLobbyLeft()
        {
            RefreshNetworkState();
        }

        public void ForceRefreshNetworkState()
        {
            RefreshNetworkState();
        }

        private void RefreshNetworkState()
        {
            var manager = YargNetworkManager.Instance;
            bool wasMultiplayer = _isMultiplayer;
            bool wasHost = _isHost;

            if (manager != null && manager.isNetworkActive)
            {
                _isMultiplayer = true;
                _isHost = manager.LocalUserIsHost();

                if (!wasMultiplayer || wasHost != _isHost)
                {
                    Debug.Log($"[MultiplayerDifficultySync] Multiplayer active: {_isMultiplayer}, Host: {_isHost}");
                }
            }
            else
            {
                _isMultiplayer = false;
                _isHost = false;

                if (wasMultiplayer)
                {
                    Debug.Log("[MultiplayerDifficultySync] Multiplayer session ended - switching to offline mode");
                }

                if (_syncedProfileIndices.Count > 0)
                {
                    _syncedProfileIndices.Clear();
                }
            }
        }

        /// <summary>
        /// Called when player selects their instrument and difficulty.
        /// Syncs the selection to the network.
        /// </summary>
        public void OnPlayerSelectionComplete(YargPlayer player)
        {
            if (!_isMultiplayer)
            {
                return;
            }

            int playerIndex = FindLocalPlayerIndex(player);
            if (playerIndex < 0)
            {
                Debug.LogWarning("[MultiplayerDifficultySync] Unable to determine local player index for selection sync");
                return;
            }

            var localPlayerData = FindLocalNetworkData(playerIndex);
            if (localPlayerData == null)
            {
                Debug.LogWarning($"[MultiplayerDifficultySync] Could not find local NetworkPlayerData for index {playerIndex}");
                return;
            }

            // Sync profile data to network
            var profile = player.Profile;
            int gameMode = (int)profile.GameMode;
            int instrument = (int)profile.CurrentInstrument;
            int difficulty = (int)profile.CurrentDifficulty;

            Debug.Log($"[MultiplayerDifficultySync] Syncing player selection - GameMode: {profile.GameMode}, Instrument: {profile.CurrentInstrument}, Difficulty: {profile.CurrentDifficulty}");

            localPlayerData.CmdSyncPlayerProfile(gameMode, instrument, difficulty);
            localPlayerData.CmdSetInstrument(instrument, difficulty);

            // Mark as ready
            localPlayerData.CmdSetReady(true);

            _syncedProfileIndices.Add(playerIndex);
        }

        /// <summary>
        /// Called when all local players have completed their selections.
        /// If we're the host and all network players are ready, start gameplay.
        /// </summary>
        public void OnAllLocalPlayersReady()
        {
            if (!_isMultiplayer)
            {
                // Single player - just load gameplay normally
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
                return;
            }

            if (_isHost)
            {
                // Host checks if all network players are ready
                var manager = YargNetworkManager.Instance;
                if (manager != null && manager.AreAllPlayersReady())
                {
                    Debug.Log("[MultiplayerDifficultySync] All players ready - starting gameplay");
                    // StartMultiplayerGameplay sends TargetStartGameplay RPC to ALL clients (including host)
                    // In listen-server scenarios the local host can call it directly; otherwise request the dedicated server
                    if (NetworkServer.active)
                    {
                        manager.StartMultiplayerGameplay();
                    }
                    else if (!RequestServerStartGameplay())
                    {
                        Debug.LogWarning("[MultiplayerDifficultySync] Failed to relay start request to server - waiting");
                    }
                }
                else
                {
                    Debug.Log("[MultiplayerDifficultySync] Waiting for other players...");
                    OnWaitingForPlayers?.Invoke("Waiting for other players...");
                }
            }
            else
            {
                Debug.Log("[MultiplayerDifficultySync] Client ready - waiting for host to start");
                // Client waits for TargetStartGameplay RPC
            }
        }

        /// <summary>
        /// Sync player profile at the start of difficulty select.
        /// This ensures the instrument from the profile is used.
        /// </summary>
        public void SyncPlayerProfileOnEntry(YargPlayer player)
        {
            if (!_isMultiplayer)
            {
                return;
            }

            int playerIndex = FindLocalPlayerIndex(player);
            if (playerIndex < 0 || _syncedProfileIndices.Contains(playerIndex))
            {
                return;
            }

            var localPlayerData = FindLocalNetworkData(playerIndex);
            if (localPlayerData == null)
            {
                return;
            }

            var profile = player.Profile;
            int gameMode = (int)profile.GameMode;
            int instrument = (int)profile.CurrentInstrument;
            int difficulty = (int)profile.CurrentDifficulty;

            Debug.Log($"[MultiplayerDifficultySync] Initial profile sync - GameMode: {profile.GameMode}, Instrument: {profile.CurrentInstrument}, Difficulty: {profile.CurrentDifficulty}");

            localPlayerData.CmdSyncPlayerProfile(gameMode, instrument, difficulty);
            localPlayerData.CmdSetInstrument(instrument, difficulty);

            _syncedProfileIndices.Add(playerIndex);
        }

        private int FindLocalPlayerIndex(YargPlayer player)
        {
            var players = PlayerContainer.Players;
            if (players == null)
            {
                return -1;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == player)
                {
                    return i;
                }
            }

            return -1;
        }

        private NetworkPlayerData FindLocalNetworkData(int playerIndex)
        {
            if (YargNetworkManager.Instance == null)
            {
                return null;
            }

            var allPlayers = YargNetworkManager.Instance.GetAllPlayers();
            foreach (var playerData in allPlayers)
            {
                if (playerData != null && playerData.IsLocalUser && playerData.PlayerIndex == playerIndex)
                {
                    return playerData;
                }
            }

            return null;
        }

        private NetworkPlayerData FindLocalHostNetworkData()
        {
            if (YargNetworkManager.Instance == null)
            {
                return null;
            }

            foreach (var playerData in YargNetworkManager.Instance.GetAllPlayers())
            {
                if (playerData != null && playerData.IsLocalUser && playerData.IsHost)
                {
                    return playerData;
                }
            }

            return null;
        }

        private bool RequestServerStartGameplay()
        {
            var localHostData = FindLocalHostNetworkData();
            if (localHostData == null)
            {
                Debug.LogWarning("[MultiplayerDifficultySync] Unable to request gameplay start - missing local host data");
                return false;
            }

            Debug.Log("[MultiplayerDifficultySync] Requesting dedicated server to start gameplay");
            localHostData.CmdRequestStartGameplay();
            return true;
        }
    }
}
