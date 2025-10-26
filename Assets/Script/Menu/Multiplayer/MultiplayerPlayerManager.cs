using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Input;
using YARG.Networking;
using YARG.Player;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Creates multiplayer-specific YargPlayer instances from NetworkPlayerData.
    /// This solves the ParallelSync issue where both instances share the same PlayerContainer.
    /// </summary>
    public class MultiplayerPlayerManager : MonoBehaviour
    {
        private static readonly Dictionary<YargPlayer, NetworkPlayerData> _playerNetworkLookup = new Dictionary<YargPlayer, NetworkPlayerData>();
        private static readonly Dictionary<NetworkPlayerData, ProfileBinding> _networkProfileBindings = new Dictionary<NetworkPlayerData, ProfileBinding>();

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

                Debug.Log($"[MultiplayerPlayerManager] Processing NetworkPlayerData: PlayerName={networkPlayer.PlayerName}, isLocal={networkPlayer.IsLocalUser}");

                // Get or create a YargPlayer for this network player
                var yargPlayer = CreatePlayerFromNetworkData(networkPlayer);
                if (yargPlayer != null)
                {
                    players.Add(yargPlayer);
                    _playerNetworkLookup[yargPlayer] = networkPlayer;
                    ApplyNetworkBindings(yargPlayer, networkPlayer);
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

            if (networkData.IsLocalUser)
            {
                // For local player, try to use the matching profile from PlayerContainer
                int index = networkData.PlayerIndex;
                if (index >= 0 && index < PlayerContainer.Players.Count)
                {
                    profile = PlayerContainer.Players[index].Profile;
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerPlayerManager] No local player found in PlayerContainer for index {index} (count={PlayerContainer.Players.Count})");
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
            var bindings = networkData.IsLocalUser
                ? ResolveLocalBindings(networkData.PlayerIndex)
                : null;
            var yargPlayer = new YargPlayer(profile, bindings);

            return yargPlayer;
        }

        /// <summary>
        /// Creates a temporary profile for a remote player based on their NetworkPlayerData.
        /// </summary>
        private static YargProfile CreateTemporaryProfile(NetworkPlayerData networkData)
        {
            var profile = new YargProfile();

            ApplyProfileSnapshot(profile, networkData);

            Debug.Log($"[MultiplayerPlayerManager] Created temp profile: {profile.Name}, GameMode: {profile.GameMode}, Instrument: {profile.CurrentInstrument}, Difficulty: {profile.CurrentDifficulty}");

            return profile;
        }

        private static ProfileBindings ResolveLocalBindings(int playerIndex)
        {
            if (PlayerContainer.Players.Count == 0)
            {
                return null;
            }

            if (playerIndex >= 0 && playerIndex < PlayerContainer.Players.Count)
            {
                return PlayerContainer.Players[playerIndex].Bindings;
            }

            return PlayerContainer.Players[0].Bindings;
        }

        private static void ApplyProfileSnapshot(YargProfile profile, NetworkPlayerData networkData)
        {
            if (profile == null || networkData == null)
            {
                return;
            }

            profile.Name = networkData.PlayerName;

            UpdateInstrumentAndDifficulty(profile, networkData.Instrument, networkData.Difficulty);
        }

        private static void ApplyNetworkBindings(YargPlayer yargPlayer, NetworkPlayerData networkData)
        {
            if (yargPlayer == null || networkData == null)
            {
                return;
            }

            if (networkData.IsLocalUser)
            {
                // Local players already rely on PlayerContainer data.
                return;
            }

            ApplyProfileSnapshot(yargPlayer.Profile, networkData);

            if (_networkProfileBindings.TryGetValue(networkData, out var existing))
            {
                networkData.OnPlayerNameChangedEvent -= existing.NameHandler;
                networkData.OnInstrumentChangedEvent -= existing.InstrumentHandler;
                networkData.OnDifficultyChangedEvent -= existing.DifficultyHandler;
            }

            var binding = new ProfileBinding
            {
                NameHandler = newName => yargPlayer.Profile.Name = newName,
                InstrumentHandler = (instrument, difficulty) => UpdateInstrumentAndDifficulty(yargPlayer.Profile, instrument, difficulty),
                DifficultyHandler = (instrument, difficulty) => UpdateInstrumentAndDifficulty(yargPlayer.Profile, instrument, difficulty)
            };

            networkData.OnPlayerNameChangedEvent += binding.NameHandler;
            networkData.OnInstrumentChangedEvent += binding.InstrumentHandler;
            networkData.OnDifficultyChangedEvent += binding.DifficultyHandler;

            _networkProfileBindings[networkData] = binding;
        }

        private sealed class ProfileBinding
        {
            public Action<string> NameHandler;
            public Action<int, int> InstrumentHandler;
            public Action<int, int> DifficultyHandler;
        }

        private static void UpdateInstrumentAndDifficulty(YargProfile profile, int instrumentValue, int difficultyValue)
        {
            if (profile == null)
            {
                return;
            }

            profile.CurrentInstrument = SanitizeInstrument(instrumentValue);
            profile.CurrentDifficulty = SanitizeDifficulty(difficultyValue);
            profile.GameMode = profile.CurrentInstrument.ToNativeGameMode();
        }

        private static Instrument SanitizeInstrument(int instrumentValue)
        {
            // Network payloads use int, while the Instrument enum is backed by byte and intentionally sparse.
            // Use Enum.IsDefined against the underlying byte value so vocals (40) and other reserved ranges survive sync.
            if (instrumentValue >= byte.MinValue && instrumentValue <= byte.MaxValue)
            {
                var candidate = (Instrument)(byte)instrumentValue;
                if (Enum.IsDefined(typeof(Instrument), candidate))
                {
                    return candidate;
                }
            }

            return Instrument.FiveFretGuitar;
        }

        private static Difficulty SanitizeDifficulty(int difficultyValue)
        {
            if (difficultyValue >= 0 && difficultyValue < Enum.GetValues(typeof(Difficulty)).Length)
            {
                return (Difficulty)difficultyValue;
            }

            return Difficulty.Expert;
        }
    }
}
