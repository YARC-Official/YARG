using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlasticBand.Devices;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Filters;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileList;
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
        private static string ProfilesBackupPath => Path.Combine(ProfilesDirectory, "profiles.json.bak");
        private static string UnloadedProfilesPath => Path.Combine(ProfilesDirectory, "profiles.json.unloaded");

        private static readonly List<YargProfile> _profiles = new();
        private static readonly List<YargPlayer>  _players  = new();
        private static readonly List<UnloadedProfile> _unloadedProfiles = new();

        private static readonly Dictionary<Guid, YargProfile>       _profilesById     = new();
        private static readonly Dictionary<YargProfile, YargPlayer> _playersByProfile = new();

        public delegate void OnPlayerAdded(YargPlayer player);
        public delegate void OnPlayerRemoved(YargPlayer player);

        public static event OnPlayerAdded PlayerAdded;
        public static event OnPlayerRemoved PlayerRemoved;

        /// <summary>
        /// A list of all of the profiles (taken or not).
        /// </summary>
        public static IReadOnlyList<YargProfile> Profiles => _profiles;

        /// <summary>
        /// A list of profile records this version of the game could not load.
        /// </summary>
        public static IReadOnlyList<UnloadedProfile> UnloadedProfiles => _unloadedProfiles;

        /// <summary>
        /// Permanently removes a set-aside profile record. The only automatic
        /// removal is a successful retry on load.
        /// </summary>
        public static bool DeleteUnloadedProfile(UnloadedProfile record)
        {
            if (!_unloadedProfiles.Remove(record))
            {
                return false;
            }

            SaveUnloadedProfiles();
            return true;
        }

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
            if (_profiles.Contains(profile))
            {
                return false;
            }

            _profiles.Add(profile);
            _profilesById.Add(profile.Id, profile);
            ActiveProfilesChanged();
            return true;
        }

        public static bool RemoveProfile(YargProfile profile)
        {
            if (!_profiles.Contains(profile))
            {
                return false;
            }

            // A profile that is taken can't be removed
            if (_playersByProfile.ContainsKey(profile))
            {
                return false;
            }

            _profiles.Remove(profile);
            _profilesById.Remove(profile.Id);
            ActiveProfilesChanged();
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
            if (!_profiles.Contains(profile))
            {
                return null;
            }

            if (IsProfileTaken(profile))
            {
                return null;
            }

            var bindings = BindingsContainer.GetBindingsForProfile(profile);
            if (resolveDevices)
            {
                bindings.ResolveDevices();
            }

            var player = new YargPlayer(profile, bindings);
            player.EnableInputs();
            _players.Add(player);
            _playersByProfile.Add(profile, player);
            ActiveProfilesChanged();
            player.RefreshPresets();
            profile.ClaimProfile();
            PlayerAdded?.Invoke(player);
            return player;
        }

        public static bool DisposePlayer(YargPlayer player)
        {
            if (!_players.Contains(player))
            {
                return false;
            }

            _players.Remove(player);
            _playersByProfile.Remove(player.Profile);

            PlayerRemoved?.Invoke(player);
            player.Dispose();
            ActiveProfilesChanged();
            return true;
        }

        public static bool SwapPlayerToProfile(YargPlayer player, YargProfile newProfile)
        {
            if (!_players.Contains(player))
            {
                return false;
            }

            if (IsProfileTaken(newProfile))
            {
                return false;
            }

            _playersByProfile.Remove(player.Profile);
            _playersByProfile.Add(newProfile, player);

            var bindings = BindingsContainer.GetBindingsForProfile(newProfile);
            player.SwapToProfile(newProfile, bindings, true);
            ActiveProfilesChanged();
            return true;
        }

        private static void ActiveProfilesChanged()
        {
            if (SettingsManager.Settings.OnlyShowPlayableSongs.Value ||
                SettingsManager.Settings.LibrarySort == SortAttribute.Playcount)
            {
                if (SettingsManager.Settings.OnlyShowPlayableSongs.Value)
                    FiltersMenu.RefreshActiveFilterPredicate();

                MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
            }

            MusicLibraryMenu.NeedsReload();

            StatsManager.Instance?.UpdateActivePlayers();
        }

        public static YargPlayer GetPlayerFromProfile(YargProfile profile)
        {
            if (!_playersByProfile.TryGetValue(profile, out var player))
            {
                return null;
            }

            return player;
        }

#nullable enable
        public static YargProfile? GetProfileForDevice(InputDevice device)
#nullable disable
        {
            var candidateProfiles = new List<YargProfile>();

            foreach (var profile in _profiles)
            {
                if (IsProfileTaken(profile))
                {
                    continue;
                }

                var bindings = BindingsContainer.GetBindingsForProfile(profile);
                if (bindings.MatchesDevice(device))
                {
                    candidateProfiles.Add(profile);
                }
            }

            // Return the profile that has the most recent LastUsed time
            return candidateProfiles.OrderByDescending(e => e.LastUsed).FirstOrDefault();
        }

        public static bool IsDeviceTaken(InputDevice device)
        {
            foreach (var player in _players)
            {
                if (player.Bindings.ContainsDevice(device))
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnDeviceAdded(InputDevice device)
        {
            foreach (var player in _players)
            {
                player.Bindings.OnDeviceAdded(device);
            }

            if (!SettingsManager.Settings.AutoCreateProfiles.Value)
            {
                return;
            }

            _ = TryCreateProfile(device);
        }

        private static void OnDeviceRemoved(InputDevice device)
        {
            foreach (var player in _players)
            {
                player.Bindings.OnDeviceRemoved(device);
            }
        }

        private static async UniTask<bool> TryCreateProfile(InputDevice device)
        {
            // Some devices don't appear in their final form immediately, so we have to wait a bit
            await UniTask.Delay(2500, true);

            if (IsDeviceTaken(device))
            {
                return false;
            }

            if (GetProfileForDevice(device) is not null)
            {
                return false;
            }

            return CreateProfileFromDevice(device);
        }

        public static bool TryConnectProfile(InputDevice device)
        {
            if (IsDeviceTaken(device))
            {
                return false;
            }

            var profile = GetProfileForDevice(device);
            if (profile is null)
            {
                return false;
            }

            CreatePlayerFromProfile(profile, true);
            return true;
        }

        /// <summary>
        /// A profile record the current version of the game could not load.
        /// Kept in a sidecar file, retried whenever the game version changes,
        /// and never deleted automatically.
        /// </summary>
        public sealed class UnloadedProfile
        {
            /// <summary>Display-only copy of the record's name; never trusted data.</summary>
            public string Name;

            /// <summary>The game version that last attempted (and failed) to load this record.</summary>
            public string LastTriedVersion;

            /// <summary>The error from the last failed attempt.</summary>
            public string LastError;

            /// <summary>The original profile record, preserved verbatim.</summary>
            public JToken Record;
        }

        /// <summary>
        /// Outcome of reading and parsing a single profiles file.
        /// </summary>
        private sealed class ProfileFileResult
        {
            public static readonly ProfileFileResult Missing = new();

            /// <summary>Raw JSON tokens of each element in the profile array.</summary>
            public List<JToken> Tokens { get; } = new();

            /// <summary>Whether the file was read and parsed as a JSON array (even an empty one).</summary>
            public bool StructurallyReadable;
        }

        public static int LoadProfiles()
        {
            _profiles.Clear();
            _profilesById.Clear();
            _unloadedProfiles.Clear();

            // Players must be disposed
            _players.ForEach(i => i.Dispose());
            _players.Clear();

            var main = LoadProfileFile(ProfilesPath, "profiles.json");
            bool fileMissing = ReferenceEquals(main, ProfileFileResult.Missing);
            bool usedBackup = false;
            ProfileFileResult source = null;

            if (!fileMissing && main.StructurallyReadable)
            {
                source = main;
            }
            else if (!fileMissing)
            {
                // The main file is unreadable or not a JSON array, so recover from the backup
                var backup = LoadProfileFile(ProfilesBackupPath, "profiles.json.bak");
                YargLogger.LogWarning("Failed to parse the main profiles file, attempting recovery from the backup.");

                if (backup.StructurallyReadable)
                {
                    source = backup;
                    usedBackup = true;
                }

                // If neither source is usable, continue with zero profiles rather than aborting
            }

            // Register profiles one at a time so a single bad record can't abort the
            // rest; records this version can't load are set aside, never deleted
            int rejectedByThisVersion = 0;

            if (source is not null)
            {
                string sourceName = usedBackup ? "profiles.json.bak" : "profiles.json";
                foreach (var token in source.Tokens)
                {
                    if (TryRegisterProfileToken(token, out _, out string error, out bool setAside))
                    {
                        continue;
                    }

                    YargLogger.LogFormatWarning("Skipped invalid profile record in {0}", sourceName + ": " + error);
                    rejectedByThisVersion++;

                    if (setAside)
                    {
                        AddUnloadedProfile(token, error);
                    }
                }
            }

            // Retry records that last failed under a different version of the game;
            // records that still fail get their attempt stamp moved forward
            LoadUnloadedProfiles();
            RetryUnloadedProfiles(out int promotedCount, out int retriedFailedCount);
            rejectedByThisVersion += retriedFailedCount;

            // Bindings loading handles orphaned records itself, so it is safe (and intended)
            // even when the valid profile count is zero
            BindingsContainer.LoadBindings();

            // Initialization must hold after every non-throwing recovery path, or no
            // profile could ever be saved again
            _isInitialized = true;

            if (usedBackup)
            {
                // Preserve the unreadable source before the sanitized rewrite replaces it
                CopyAsideFailedSource(ProfilesPath, ProfilesPath + ".invalid");
            }

            if (usedBackup || rejectedByThisVersion > 0 || promotedCount > 0)
            {
                // Rewrite the active files from the sanitized set; set-aside records
                // live on in the sidecar, so nothing is lost
                SaveProfiles(false);
                SaveBackupProfiles();
            }
            else if (!fileMissing)
            {
                // Loading was good, so refresh the backup as usual
                SaveBackupProfiles();
            }

            if (rejectedByThisVersion > 0 || promotedCount > 0 || retriedFailedCount > 0)
            {
                SaveUnloadedProfiles();
            }

            if (rejectedByThisVersion > 0)
            {
                // Deferred text: the toast is queued during startup, before
                // localization has loaded, so resolve the key when shown
                ToastManager.ToastWarning(() =>
                    Localize.KeyFormat("Menu.Toast.ProfileLoadWarning", rejectedByThisVersion));
            }

            return _profiles.Count;
        }

        /// <summary>
        /// Converts and registers a single profile record. Any record-local failure
        /// (deserialization, validation, migration) returns false with an error
        /// message instead of throwing. Duplicate IDs also return false, but with
        /// <paramref name="setAside"/> cleared since retrying them cannot help.
        /// </summary>
        private static bool TryRegisterProfileToken(JToken token, out YargProfile profile, out string error, out bool setAside)
        {
            profile = null;
            error = string.Empty;
            setAside = true;

            try
            {
                // This includes OnDeserialized validation failures, malformed
                // individual records, and null elements
                profile = token.ToObject<YargProfile>();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (profile is null)
            {
                error = "Profile record is null.";
                return false;
            }

            try
            {
                profile.GrandfatherIn();
            }
            catch (Exception ex)
            {
                error = "Migration failed: " + ex.Message;
                profile = null;
                return false;
            }

            if (_profilesById.ContainsKey(profile.Id))
            {
                error = $"Duplicate profile ID {profile.Id}.";
                profile = null;
                setAside = false;
                return false;
            }

            _profiles.Add(profile);
            _profilesById.Add(profile.Id, profile);
            return true;
        }

        /// <summary>
        /// Reattempts sidecar records whose last failure was under a different
        /// game version. Promotions become normal profiles; records that still
        /// fail get their attempt stamp moved to the current version.
        /// </summary>
        private static void RetryUnloadedProfiles(out int promotedCount, out int retriedFailedCount)
        {
            promotedCount = 0;
            retriedFailedCount = 0;

            string currentVersion = GlobalVariables.Instance.CurrentVersion;

            for (int i = _unloadedProfiles.Count - 1; i >= 0; i--)
            {
                var record = _unloadedProfiles[i];

                if (record.Record is null)
                {
                    // Malformed sidecar entry; leave it alone rather than delete user data
                    continue;
                }

                // Only reattempt records that failed under a different build
                if (record.LastTriedVersion == currentVersion)
                {
                    continue;
                }

                if (TryRegisterProfileToken(record.Record, out var profile, out string error, out bool setAside))
                {
                    YargLogger.LogInfo($"Profile '{profile.Name}' loaded after being set aside by version {record.LastTriedVersion}.");
                    _unloadedProfiles.RemoveAt(i);
                    promotedCount++;
                }
                else if (setAside)
                {
                    YargLogger.LogWarning($"Profile '{record.Name}' (set aside by {record.LastTriedVersion}) still fails under {currentVersion}: {error}");
                    record.LastTriedVersion = currentVersion;
                    record.LastError = error;
                    retriedFailedCount++;
                }
                else
                {
                    YargLogger.LogWarning($"Profile '{record.Name}' (set aside by {record.LastTriedVersion}) cannot be registered yet: {error}");
                }
            }
        }

        private static void AddUnloadedProfile(JToken token, string error)
        {
            var name = (token as JObject)?["Name"]?.Value<string>();
            _unloadedProfiles.Add(new UnloadedProfile
            {
                Name = string.IsNullOrEmpty(name) ? "(unknown)" : name,
                LastTriedVersion = GlobalVariables.Instance.CurrentVersion,
                LastError = error,
                Record = token,
            });
        }

        private static void LoadUnloadedProfiles()
        {
            if (!File.Exists(UnloadedProfilesPath))
            {
                return;
            }

            try
            {
                var records = JsonConvert.DeserializeObject<List<UnloadedProfile>>(
                    File.ReadAllText(UnloadedProfilesPath));

                if (records is not null)
                {
                    _unloadedProfiles.AddRange(records);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogError("Failed to read the unloaded-profiles sidecar: " + ex.Message);
            }
        }

        public static void SaveUnloadedProfiles()
        {
            try
            {
                if (_unloadedProfiles.Count == 0)
                {
                    if (File.Exists(UnloadedProfilesPath))
                    {
                        File.Delete(UnloadedProfilesPath);
                    }

                    return;
                }

                File.WriteAllText(UnloadedProfilesPath,
                    JsonConvert.SerializeObject(_unloadedProfiles, Formatting.Indented));
            }
            catch (Exception ex)
            {
                YargLogger.LogError("Failed to write the unloaded-profiles sidecar: " + ex.Message);
            }
        }

        /// <summary>
        /// Parses a profiles file element-by-element so one invalid record
        /// cannot discard the valid ones.
        /// </summary>
        private static ProfileFileResult LoadProfileFile(string path, string name)
        {
            if (!File.Exists(path))
            {
                return ProfileFileResult.Missing;
            }

            string profilesJson;
            try
            {
                profilesJson = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Failed to read {name}: {ex.Message}");
                return new ProfileFileResult();
            }

            JArray root;
            try
            {
                if (JToken.Parse(profilesJson) is not JArray array)
                {
                    YargLogger.LogFormatError("{0} does not contain a JSON array.", name);
                    return new ProfileFileResult();
                }

                root = array;
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Failed to parse {name}: {ex.Message}");
                return new ProfileFileResult();
            }

            var result = new ProfileFileResult
            {
                StructurallyReadable = true,
            };

            // Records are converted at registration time so their raw form can be
            // preserved verbatim if they fail
            foreach (var token in root)
            {
                result.Tokens.Add(token);
            }

            return result;
        }

        private static void CopyAsideFailedSource(string sourcePath, string destPath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return;
                }

                if (File.Exists(destPath))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    destPath += "." + timestamp;
                }

                File.Copy(sourcePath, destPath);
                YargLogger.LogFormatWarning("Preserved failed profile source as {0}.", Path.GetFileName(destPath));
            }
            catch (Exception ex)
            {
                YargLogger.LogFormatError("Failed to preserve failed profile source: {0}", ex.Message);
            }
        }

        public static int SaveProfiles(bool updateOrder = true)
        {
            if (!_isInitialized)
            {
                YargLogger.LogWarning("Profiles could not be saved as they were not loaded");
                return 0;
            }

            if (updateOrder)
            {
                UpdateProfileOrder();
            }

            string profilesJson = JsonConvert.SerializeObject(_profiles, Formatting.Indented);

            // We do this dance with the temporary file to avoid the possibility of corruption during saving
            string tempPath = Path.Combine(ProfilesDirectory, Path.GetRandomFileName());

            try
            {
                File.WriteAllText(tempPath, profilesJson);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatError("Failed to write profiles to file: {0}", e.Message);
                return 0;
            }

            // Verify that the newly written file is valid before replacing the old one
            int? profileCount = VerifyProfileFile(tempPath);
            if (profileCount is null || profileCount != _profiles.Count)
            {
                YargLogger.LogFormatError("Failed to verify new profiles file: {0} profiles were expected, but {1} were found", _profiles.Count, profileCount ?? -1);
                File.Delete(tempPath);
                return 0;
            }

            if (File.Exists(ProfilesPath))
            {
                File.Delete(ProfilesPath);
            }

            File.Move(tempPath, ProfilesPath);

            BindingsContainer.SaveBindings();

            return _profiles.Count;
        }

        private static bool SaveBackupProfiles()
        {
            if (!_isInitialized)
            {
                YargLogger.LogWarning("Profiles could not be saved as they were not loaded");
                return false;
            }

            string profilesJson = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
            File.WriteAllText(ProfilesBackupPath, profilesJson);

            return true;
        }

        private static int? VerifyProfileFile(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            List<YargProfile> profiles;

            try
            {
                string profilesJson = File.ReadAllText(path);
                profiles = JsonConvert.DeserializeObject<List<YargProfile>>(profilesJson);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatError("Failed to verify profile file: {0}", e.Message);
                return null;
            }

            return profiles.Count;
        }

        public static void Destroy()
        {
            // Can't `foreach` when modifying a collection, so this will do instead
            while (_players.Count > 0)
            {
                DisposePlayer(_players[0]);
            }
        }

        public static void EnsureValidInstruments()
        {
            foreach (var profile in _profiles)
            {
                profile.EnsureValidInstrument();
            }
        }

        public static bool HasConnectedKeyboardProfile()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            foreach (var player in _players)
            {
                if (player.InputsEnabled && player.Bindings.ContainsDevice(keyboard))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool OnlyHasBotsActive()
        {
            foreach (var player in _players)
            {
                if (!player.Profile.IsBot)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasAnyBotsActive()
        {
            return _players.Exists(i => i.Profile.IsBot);
        }

        public static int GetPlayerIndex(YargPlayer player)
        {
            int index = _players.IndexOf(player);
            if (index == -1)
            {
                throw new ArgumentException("Player not found in the active player list");
            }

            return index;
        }

        public static void MoveUp(YargPlayer player)
        {
            int index = GetPlayerIndex(player);
            if (index == 0)
            {
                return;
            }

            _players.RemoveAt(index);
            _players.Insert(index - 1, player);
        }

        public static void MoveDown(YargPlayer player)
        {
            int index = GetPlayerIndex(player);
            if (index == _players.Count - 1)
            {
                return;
            }

            _players.RemoveAt(index);
            _players.Insert(index + 1, player);
        }

        public static void UpdateProfileOrder()
        {
            ClearProfileOrder();

            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Profile.AutoConnectOrder = i;
            }
        }

        public static void ClearProfileOrder()
        {
            foreach (var profile in Profiles)
            {
                profile.AutoConnectOrder = null;
            }
        }

        public static void AutoConnectProfiles()
        {
            foreach (var profile in Profiles.Where(e => e.AutoConnectOrder != null).OrderBy(e => e.AutoConnectOrder))
            {
                CreatePlayerFromProfile(profile, true);
            }
        }

        private static bool CreateProfileFromDevice(InputDevice device)
        {
            if (IsDeviceTaken(device))
            {
                return false;
            }

            GameMode gameMode = default;
            string profileName = string.Empty;

            if (device is FiveFretGuitar)
            {
                gameMode = GameMode.FiveFretGuitar;
                profileName = "New Guitar Profile";
            }
            else if (device is FourLaneDrumkit)
            {
                gameMode = GameMode.FourLaneDrums;
                profileName = "New Drums Profile";
            }
            else if (device is FiveLaneDrumkit)
            {
                gameMode = GameMode.FiveLaneDrums;
                profileName = "New Drums Profile";
            }
            else if (device is ProKeyboard)
            {
                gameMode = GameMode.ProKeys;
                profileName = "New Keys Profile";
            }
            else
            {
                // Filter out keyboard and mouse devices for the purposes of this message, otherwise we're just
                // making noise about nothing for most players
                if (device is Keyboard or Mouse or Pen)
                {
                    return false;
                }

                // TODO: Figure out why this triggers for non-input devices like stage kits so we can enable this
                // var failMessage = Localize.KeyFormat("Menu.Toast.UnsupportedDevice", device.displayName);
                // ToastManager.ToastWarning(failMessage);
                return false;
            }

            var newProfile = new YargProfile
            {
                Name = ProfileListMenu.GetUniqueProfileName(profileName),
                NoteSpeed = 5,
                HighwayLength = 1,
                GameMode = gameMode
            };

            AddProfile(newProfile);

            var player = CreatePlayerFromProfile(newProfile, false);
            if (player is null)
            {
                YargLogger.LogFormatError("Failed to connect profile {0}!", newProfile.Name);
                return false;
            }

            player.Bindings.AddDevice(device);

            if (!player.Bindings.ContainsBindingsForDevice(device))
            {
                player.Bindings.SetDefaultBinds(device);
            }

            var successMessage = Localize.KeyFormat("Menu.Toast.ProfileCreated", device.displayName);
            ToastManager.ToastSuccess(successMessage);
            return true;
        }
    }
}
