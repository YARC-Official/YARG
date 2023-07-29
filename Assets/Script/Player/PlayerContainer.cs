using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Helpers;

namespace YARG.Player
{
    /// <summary>
    /// A class that manages all of the <see cref="YargProfile"/>s and <see cref="YargPlayer"/>s.
    /// <br/><br/>
    /// <see cref="YargProfile"/>s are used to store and serialize profile settings, names, etc.
    /// Once a profile is "taken," it turns into a <see cref="YargPlayer"/>.
    /// </summary>
    public static class PlayerContainer
    {
        private const string  PROFILES_FILE = "profiles.json";
        private static string ProfilesPath => Path.Combine(PathHelper.PersistentDataPath, PROFILES_FILE);

        private static readonly List<YargProfile> _profiles = new();
        private static readonly List<YargPlayer> _players = new();

        private static readonly Dictionary<YargProfile, YargPlayer> _playersByProfile = new();

        /// <summary>
        /// A list of all of the profiles (taken or not).
        /// </summary>
        public static IReadOnlyList<YargProfile> Profiles => _profiles;

        /// <summary>
        /// A list of all of the active players.
        /// </summary>
        public static IReadOnlyList<YargPlayer> Players => _players;

        public static bool AddProfile(YargProfile profile)
        {
            if (_profiles.Contains(profile))
                return false;

            _profiles.Add(profile);
            return true;
        }

        public static bool RemoveProfile(YargProfile profile)
        {
            if (!_profiles.Contains(profile))
                return false;

            // A profile that is taken can't be removed
            if (_playersByProfile.ContainsKey(profile))
                return false;

            _profiles.Remove(profile);
            return true;
        }

        public static bool IsProfileTaken(YargProfile profile)
        {
            return _playersByProfile.ContainsKey(profile);
        }

        public static YargPlayer CreatePlayerFromProfile(YargProfile profile)
        {
            if (!_profiles.Contains(profile))
                return null;

            if (IsProfileTaken(profile))
                return null;

            var player = new YargPlayer(profile);
            _players.Add(player);
            _playersByProfile.Add(profile, player);

            return player;
        }

        public static bool DisposePlayer(YargPlayer player)
        {
            if (!_players.Contains(player))
                return false;

            _players.Remove(player);
            _playersByProfile.Remove(player.Profile);

            player.Dispose();

            return true;
        }

        public static bool SwapPlayerToProfile(YargPlayer player, YargProfile newProfile)
        {
            if (!_players.Contains(player))
                return false;

            if (IsProfileTaken(newProfile))
                return false;

            _playersByProfile.Remove(player.Profile);
            _playersByProfile.Add(newProfile, player);

            player.SwapToProfile(newProfile);

            return true;
        }

        public static YargPlayer GetPlayerFromProfile(YargProfile profile)
        {
            if (!_playersByProfile.TryGetValue(profile, out var player))
                return null;

            return player;
        }

        public static int LoadProfiles()
        {
            _profiles.Clear();

            // Players must be disposed
            _players.ForEach(i => i.Dispose());
            _players.Clear();

            string profilesPath = ProfilesPath;
            if (!File.Exists(profilesPath))
                return 0;

            string profilesJson = File.ReadAllText(profilesPath);
            var profiles = JsonConvert.DeserializeObject<List<YargProfile>>(profilesJson);
            if (profiles is not null)
                _profiles.AddRange(profiles);

            return _profiles.Count;
        }

        public static int SaveProfiles()
        {
            string profilesJson = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
            File.WriteAllText(ProfilesPath, profilesJson);

            return _profiles.Count;
        }
    }
}