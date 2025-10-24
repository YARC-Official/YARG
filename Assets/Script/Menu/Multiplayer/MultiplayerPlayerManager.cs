using UnityEngine;
using YARG.Networking;
using YARG.Player;
using YARG.Core.Game;
using System.Collections.Generic;
using YARG.Core;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Creates multiplayer-specific YargPlayer instances from NetworkPlayerData.
    /// This solves the ParallelSync issue where both instances share the same PlayerContainer.
    /// </summary>
    public class MultiplayerPlayerManager : MonoBehaviour
    {
        private static readonly Dictionary<YargPlayer, NetworkPlayerData> _playerNetworkLookup = new Dictionary<YargPlayer, NetworkPlayerData>();

        /// <summary>
        /// Gets the cached map between the runtime <see cref="YargPlayer"/> objects and their backing
        /// <see cref="NetworkPlayerData"/> instances. This is populated every time
        /// <see cref="CreateMultiplayerPlayers"/> is called.
        /// </summary>
        public static IReadOnlyDictionary<YargPlayer, NetworkPlayerData> PlayerNetworkMap => _playerNetworkLookup;

        /// <summary>
        /// Creates YargPlayer instances for all connected network players.
        /// This is used in multiplayer to replace PlayerContainer.Players.
        /// </summary>
        public static List<YargPlayer> CreateMultiplayerPlayers()
        {
            var players = new List<YargPlayer>();
            _playerNetworkLookup.Clear();

            if (YargNetworkManager.Instance == null)
            {
                Debug.LogWarning("[MultiplayerPlayerManager] YargNetworkManager.Instance is NULL!");
                return players;
            }

            if (!YargNetworkManager.Instance.isNetworkActive)
            {
                Debug.LogWarning("[MultiplayerPlayerManager] Network is not active!");
                return players;
            }

            var networkPlayers = YargNetworkManager.Instance.GetAllPlayers();
            Debug.Log($"[MultiplayerPlayerManager] Found {networkPlayers.Count} NetworkPlayerData objects");

            foreach (var networkPlayer in networkPlayers)
            {
                if (networkPlayer == null)
                {
                    Debug.LogWarning("[MultiplayerPlayerManager] Skipping null NetworkPlayerData - object was destroyed!");
                    continue;
                }
                
                // Check if the GameObject itself is valid
                if (networkPlayer.gameObject == null)
                {
                    Debug.LogWarning($"[MultiplayerPlayerManager] NetworkPlayerData {networkPlayer.PlayerName} has null gameObject - being destroyed!");
                    continue;
                }

                Debug.Log($"[MultiplayerPlayerManager] Processing NetworkPlayerData: PlayerName={networkPlayer.PlayerName}, isLocalPlayer={networkPlayer.isLocalPlayer}");

                // Get or create a YargPlayer for this network player
                var yargPlayer = CreatePlayerFromNetworkData(networkPlayer);
                if (yargPlayer != null)
                {
                    players.Add(yargPlayer);
                    _playerNetworkLookup[yargPlayer] = networkPlayer;
                    Debug.Log($"[MultiplayerPlayerManager] Created player: {yargPlayer.Profile.Name}, Instrument: {yargPlayer.Profile.CurrentInstrument}");
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerPlayerManager] Failed to create YargPlayer for {networkPlayer.PlayerName}");
                }
            }

            Debug.Log($"[MultiplayerPlayerManager] Created {players.Count} multiplayer players from {networkPlayers.Count} network objects");
            return players;
        }

        /// <summary>
        /// Try to resolve the <see cref="NetworkPlayerData"/> associated with the provided runtime
        /// <see cref="YargPlayer"/> instance.
        /// </summary>
        public static bool TryGetNetworkPlayer(YargPlayer yargPlayer, out NetworkPlayerData networkPlayer)
        {
            return _playerNetworkLookup.TryGetValue(yargPlayer, out networkPlayer);
        }

        /// <summary>
        /// Creates a YargPlayer from NetworkPlayerData.
        /// </summary>
        private static YargPlayer CreatePlayerFromNetworkData(NetworkPlayerData networkData)
        {
            // Get the profile from the network data
            YargProfile profile;

            if (networkData.isLocalPlayer)
            {
                // For local player, use the actual profile from PlayerContainer
                if (PlayerContainer.Players.Count > 0)
                {
                    profile = PlayerContainer.Players[0].Profile;
                }
                else
                {
                    Debug.LogWarning("[MultiplayerPlayerManager] No local players in PlayerContainer!");
                    return null;
                }
            }
            else
            {
                // For remote players, create a temporary profile from network data
                profile = CreateTemporaryProfile(networkData);
            }

            // Create the player
            // Note: We don't add this to PlayerContainer because it's a temporary multiplayer-only player
            var bindings = networkData.isLocalPlayer ? PlayerContainer.Players[0].Bindings : null;
            var yargPlayer = new YargPlayer(profile, bindings);

            return yargPlayer;
        }

        /// <summary>
        /// Creates a temporary profile for a remote player based on their NetworkPlayerData.
        /// </summary>
        private static YargProfile CreateTemporaryProfile(NetworkPlayerData networkData)
        {
            var profile = new YargProfile();
            profile.Name = networkData.PlayerName;

            // Set instrument from network data
            if (networkData.Instrument >= 0)
            {
                profile.CurrentInstrument = (Instrument)networkData.Instrument;
            }
            else
            {
                profile.CurrentInstrument = Instrument.FiveFretGuitar; // Default
            }

            // Set difficulty from network data
            if (networkData.Difficulty >= 0)
            {
                profile.CurrentDifficulty = (Difficulty)networkData.Difficulty;
            }
            else
            {
                profile.CurrentDifficulty = Difficulty.Expert; // Default
            }

            // Determine game mode from instrument
            profile.GameMode = profile.CurrentInstrument.ToNativeGameMode();

            Debug.Log($"[MultiplayerPlayerManager] Created temp profile: {profile.Name}, GameMode: {profile.GameMode}, Instrument: {profile.CurrentInstrument}, Difficulty: {profile.CurrentDifficulty}");

            return profile;
        }
    }
}
