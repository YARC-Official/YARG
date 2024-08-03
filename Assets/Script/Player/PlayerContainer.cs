using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Menu.MusicLibrary;
using YARG.Settings;
using YARG.Song;

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
        public  static string ProfilesDirectory => Path.Combine(PathHelper.PersistentDataPath, "profiles");
        private static string ProfilesPath      => Path.Combine(ProfilesDirectory, "profiles.json");

        private static readonly List<YargProfile> _profiles = new();
        private static readonly List<YargPlayer>  _players  = new();

        private static readonly Dictionary<Guid, YargProfile>       _profilesById     = new();
        private static readonly Dictionary<YargProfile, YargPlayer> _playersByProfile = new();

        /// <summary>
        /// A list of all of the profiles (taken or not).
        /// </summary>
        public static IReadOnlyList<YargProfile> Profiles => _profiles;

        /// <summary>
        /// A list of all of the active players.
        /// </summary>
        public static IReadOnlyList<YargPlayer> Players => _players;

        /// <summary>
        /// An enumerator over the list of all of the active players.
        /// </summary>
        public static List<YargPlayer>.Enumerator PlayerEnumerator => _players.GetEnumerator();

        private static bool _isInitialized;

        static PlayerContainer()
        {
            // Make sure the folder exists to prevent errors
            Directory.CreateDirectory(ProfilesDirectory);

            InputManager.DeviceAdded += OnDeviceAdded;
            InputManager.DeviceRemoved += OnDeviceRemoved;
        }

        public static bool AddProfile(YargProfile profile)
        {
            if (_profiles.Contains(profile)) return false;

            _profiles.Add(profile);
            _profilesById.Add(profile.Id, profile);
            ResetPlayableSongs();
            return true;
        }

        public static bool RemoveProfile(YargProfile profile)
        {
            if (!_profiles.Contains(profile)) return false;

            // A profile that is taken can't be removed
            if (_playersByProfile.ContainsKey(profile)) return false;

            _profiles.Remove(profile);
            _profilesById.Remove(profile.Id);
            ResetPlayableSongs();
            return true;
        }

        public static YargProfile GetProfileById(Guid id)
        {
            return _profilesById.GetValueOrDefault(id);
        }

        public static bool IsProfileTaken(YargProfile profile)
        {
            return _playersByProfile.ContainsKey(profile);
        }

        public static YargPlayer CreatePlayerFromProfile(YargProfile profile, bool resolveDevices)
        {
            if (!_profiles.Contains(profile)) return null;

            if (IsProfileTaken(profile)) return null;

            var bindings = BindingsContainer.GetBindingsForProfile(profile);
            var player = new YargPlayer(profile, bindings, resolveDevices);
            player.EnableInputs();
            _players.Add(player);
            _playersByProfile.Add(profile, player);
            ResetPlayableSongs();
            return player;
        }

        public static bool DisposePlayer(YargPlayer player)
        {
            if (!_players.Contains(player)) return false;

            _players.Remove(player);
            _playersByProfile.Remove(player.Profile);

            player.Dispose();
            ResetPlayableSongs();
            return true;
        }

        public static bool SwapPlayerToProfile(YargPlayer player, YargProfile newProfile)
        {
            if (!_players.Contains(player)) return false;

            if (IsProfileTaken(newProfile)) return false;

            _playersByProfile.Remove(player.Profile);
            _playersByProfile.Add(newProfile, player);

            var bindings = BindingsContainer.GetBindingsForProfile(newProfile);
            player.SwapToProfile(newProfile, bindings, true);
            ResetPlayableSongs();
            return true;
        }

        private static void ResetPlayableSongs()
        {
            if (SettingsManager.Settings.LibrarySort == SortAttribute.Playable)
            {
                MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
            }
        }

        public static YargPlayer GetPlayerFromProfile(YargProfile profile)
        {
            if (!_playersByProfile.TryGetValue(profile, out var player)) return null;

            return player;
        }

        public static bool IsDeviceTaken(InputDevice device)
        {
            foreach (var player in _players)
            {
                if (player.Bindings.ContainsDevice(device)) return true;
            }

            return false;
        }

        private static void OnDeviceAdded(InputDevice device)
        {
            foreach (var player in _players)
            {
                player.Bindings.OnDeviceAdded(device);
            }
        }

        private static void OnDeviceRemoved(InputDevice device)
        {
            foreach (var player in _players)
            {
                player.Bindings.OnDeviceRemoved(device);
            }
        }

        public static int LoadProfiles()
        {
            _profiles.Clear();
            _profilesById.Clear();

            // Players must be disposed
            _players.ForEach(i => i.Dispose());
            _players.Clear();

            string profilesPath = ProfilesPath;
            if (!File.Exists(profilesPath))
            {
                // If the file doesn't exist, then there are no profiles
                _isInitialized = true;
                return 0;
            }

            string profilesJson = File.ReadAllText(profilesPath);
            List<YargProfile> profiles;
            try
            {
                profiles = JsonConvert.DeserializeObject<List<YargProfile>>(profilesJson);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while loading profiles! Bindings loading will be skipped.");
                return 0;
            }

            if (profiles is null)
            {
                YargLogger.LogWarning("Failed to load profiles! Bindings loading will be skipped.");
                return 0;
            }

            _profiles.AddRange(profiles);

            // Store profiles by ID
            foreach (var profile in _profiles)
            {
                _profilesById.Add(profile.Id, profile);
            }

            BindingsContainer.LoadBindings();

            _isInitialized = true;

            return _profiles.Count;
        }

        public static int SaveProfiles()
        {
            if (!_isInitialized)
            {
                YargLogger.LogWarning("Profiles could not be saved as they were not loaded");
                return 0;
            }

            string profilesJson = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
            File.WriteAllText(ProfilesPath, profilesJson);

            BindingsContainer.SaveBindings();

            return _profiles.Count;
        }

        public static void Destroy()
        {
            // Can't `foreach` when modifying a collection, so this will do instead
            while (_players.Count > 0)
            {
                DisposePlayer(_players[0]);
            }
        }
    }
}