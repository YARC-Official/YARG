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
        private bool _hasSyncedProfile;

        /// <summary>
        /// Event fired when host is waiting for other players.
        /// Parameter is the waiting message to display.
        /// </summary>
        public System.Action<string> OnWaitingForPlayers;

        private void Start()
        {
            // Check if we're in multiplayer mode
            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive)
            {
                _isMultiplayer = false;
                return;
            }

            _isMultiplayer = true;
            _isHost = YargNetworkManager.Instance.IsHosting;

            Debug.Log($"[MultiplayerDifficultySync] Initialized - Multiplayer: {_isMultiplayer}, Host: {_isHost}");
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

            // Get the local player's NetworkPlayerData
            var localPlayerData = GetLocalPlayerData();
            if (localPlayerData == null)
            {
                Debug.LogWarning("[MultiplayerDifficultySync] Could not find local NetworkPlayerData");
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

            _hasSyncedProfile = true;
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
                if (YargNetworkManager.Instance.AreAllPlayersReady())
                {
                    Debug.Log("[MultiplayerDifficultySync] All players ready - starting gameplay");
                    // StartMultiplayerGameplay sends TargetStartGameplay RPC to ALL clients (including host)
                    // So we DON'T load the scene locally - let the RPC handler do it for everyone
                    YargNetworkManager.Instance.StartMultiplayerGameplay();
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
            if (!_isMultiplayer || _hasSyncedProfile)
            {
                return;
            }

            var localPlayerData = GetLocalPlayerData();
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
        }

        private NetworkPlayerData GetLocalPlayerData()
        {
            if (YargNetworkManager.Instance == null)
            {
                return null;
            }

            // Find the local player's NetworkPlayerData
            var allPlayers = YargNetworkManager.Instance.GetAllPlayers();
            foreach (var playerData in allPlayers)
            {
                if (playerData != null && playerData.isLocalPlayer)
                {
                    return playerData;
                }
            }

            return null;
        }
    }
}
