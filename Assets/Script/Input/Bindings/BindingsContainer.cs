using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;
using YARG.Player;

namespace YARG.Input.Bindings
{
    /// <summary>
    /// Manages all of the <see cref="PlayerDeviceInfo"/> for <see cref="YargProfile"/>/<see cref="YargPlayer"/>s.
    /// </summary>
    public static class BindingsContainer
    {
        private static string BindingsPath => Path.Combine(PlayerContainer.ProfilesDirectory, "bindings.json");
        private static string BindingsBackupPath => Path.Combine(PlayerContainer.ProfilesDirectory, "bindings.json.bak");

        private static readonly Dictionary<Guid, PlayerDeviceInfo> _profileDeviceInfo = new(); // GUID is YargProfile GUID

        private static readonly Dictionary<Guid, ReusableBindingSet> _allReusableBindingSetsByGuid = new(); // GUID is ReusableBindingSet GUID
        public static IEnumerable<PlayerDeviceInfo> AllPlayerDeviceInfo => _profileDeviceInfo.Values;

        private static readonly Dictionary<ControllerFamily, Dictionary<GameMode, List<ReusableBindingSet>>> _reusableBindingSetsByControllerFamily = new();

        public static PlayerDeviceInfo GetDeviceInfoForProfile(YargProfile profile)
        {
            if (!_profileDeviceInfo.TryGetValue(profile.Id, out var bindings))
            {
                // Nothing to deserialize; constructor will apply defaults
                bindings = new(profile);
                _profileDeviceInfo.Add(profile.Id, bindings);
            }

            return bindings;
        }

        public static Dictionary<GameMode, List<ReusableBindingSet>> GetBindingSetsForControllerFamily(ControllerFamily controllerFamily)
        {
            return _reusableBindingSetsByControllerFamily.GetValueOrDefault(controllerFamily, new());
        }

        public static List<GameMode> GetTypicalGameModesForControllerFamily(ControllerFamily controllerFamily)
        {
            return controllerFamily switch {
                ControllerFamily.FiveFretGuitar => new() { GameMode.FiveFretGuitar },
                ControllerFamily.SixFretGuitar => new() { GameMode.SixFretGuitar },
                ControllerFamily.FourLaneDrumkit => new() { GameMode.FourLaneDrums },
                ControllerFamily.FiveLaneDrumkit => new() { GameMode.FiveLaneDrums },
                ControllerFamily.ProKeyboard => new() { GameMode.ProKeys },
                ControllerFamily.MidiDevice => new() { GameMode.ProKeys, GameMode.EliteDrums },
                ControllerFamily.ProGuitar => new() { GameMode.ProGuitar, GameMode.FiveFretGuitar },
                ControllerFamily.Turntable => new() { /* GameMode.Dj */ },
                ControllerFamily.Gamepad => new() { GameMode.Vocals, GameMode.FiveFretGuitar, GameMode.SixFretGuitar, GameMode.FourLaneDrums, GameMode.FiveLaneDrums, GameMode.EliteDrums },
                ControllerFamily.ComputerKeyboard => new() { GameMode.FiveFretGuitar, GameMode.SixFretGuitar, GameMode.FourLaneDrums, GameMode.FiveLaneDrums, GameMode.EliteDrums, GameMode.ProKeys, GameMode.Vocals },
                ControllerFamily.Mouse => new() { GameMode.Vocals },
                _ => new() { }
            };
        }

        public static List<ReusableBindingSet> GetBindingSetsForControllerInMode(ControllerFamily controllerFamily, GameMode mode)
        {
            if (_reusableBindingSetsByControllerFamily.TryGetValue(controllerFamily, out var familySets))
            {
                return familySets.GetValueOrDefault(mode, new());
            }

            return new();
        }


        public static bool TryGetBindingSetById(Guid guid, out ReusableBindingSet bindingSet)
        {
            if (_allReusableBindingSetsByGuid.TryGetValue(guid, out bindingSet))
            {
                return true;
            }

            bindingSet = null;
            return false;
        }

        public static void LoadBindings()
        {
            ReusableBindingSetDefaults.InitializeDefaults();

            bool usedBackup = false;

            _profileDeviceInfo.Clear();

            string bindingsPath = BindingsPath;
            if (!File.Exists(bindingsPath))
                return;

            var bindings = BindingSerialization.DeserializeBindings(bindingsPath);
            if (bindings is null)
            {
                YargLogger.LogWarning("Failed to load bindings! Attempting to load backup.");

                bindings = BindingSerialization.DeserializeBindings(BindingsBackupPath);
                if (bindings is null)
                {
                    YargLogger.LogWarning("Failed to load bindings from backup!");
                    return;
                }
                usedBackup = true;
            }

            foreach (var (guid, serializedReusableBindingSet) in bindings.ReusableBindingSets)
            {
                var bindingSet = new ReusableBindingSet(serializedReusableBindingSet);
                AddBindingSet(bindingSet);
            }

            foreach (var (id, serialized) in bindings.Profiles)
            {
                var profile = PlayerContainer.GetProfileById(id);
                if (profile is null)
                {
                    YargLogger.LogFormatWarning("Bindings exist for profile ID {0}, but the corresponding profile was not found! Bindings will be discarded.", id);
                    continue;
                }

                // Don't load bindings for bots
                if (profile.IsBot)
                    continue;

                var deserialized = PlayerDeviceInfo.Deserialize(profile, serialized);
                _profileDeviceInfo.Add(id, deserialized);
            }

            // If we used the backup save the backup data to the main path, otherwise save main to backup
            if (usedBackup)
            {
                SaveBindings(BindingsPath);
            }
            else
            {
                SaveBindings(BindingsBackupPath);
            }
        }

        public static int SaveBindings(string path = null)
        {
            path ??= BindingsPath;

            var serialized = new SerializedBindings();
            foreach (var (profileId, deviceInfo) in _profileDeviceInfo)
            {
                var profile = PlayerContainer.GetProfileById(profileId);
                if (profile is null || profile.IsBot) // Don't save device info for bots
                    continue;

                serialized.Profiles[profileId] = deviceInfo.Serialize();
            }

            foreach (var bindingSet in _allReusableBindingSetsByGuid.Values)
            {
                if (!bindingSet.IsHardcoded)
                {
                    serialized.ReusableBindingSets[bindingSet.Guid] = bindingSet.Serialize();
                }
            }

            BindingSerialization.SerializeBindings(serialized, path);
            return _profileDeviceInfo.Count;
        }

        public static void AddBindingSet(ReusableBindingSet newSet)
        {
            _allReusableBindingSetsByGuid[newSet.Guid] = newSet;

            if (!_reusableBindingSetsByControllerFamily.ContainsKey(newSet.ControllerFamily))
            {
                _reusableBindingSetsByControllerFamily[newSet.ControllerFamily] = new();
            }

            if (!_reusableBindingSetsByControllerFamily[newSet.ControllerFamily].ContainsKey(newSet.Mode))
            {
                _reusableBindingSetsByControllerFamily[newSet.ControllerFamily][newSet.Mode] = new();
            }

            _reusableBindingSetsByControllerFamily[newSet.ControllerFamily][newSet.Mode].Add(newSet);
        }

        public static void DeleteBindingSet(ReusableBindingSet bindingSet)
        {
            _allReusableBindingSetsByGuid.Remove(bindingSet.Guid);
            _reusableBindingSetsByControllerFamily[bindingSet.ControllerFamily][bindingSet.Mode].Remove(bindingSet);

            if (_reusableBindingSetsByControllerFamily[bindingSet.ControllerFamily][bindingSet.Mode].Count is 0)
            {
                _reusableBindingSetsByControllerFamily[bindingSet.ControllerFamily].Remove(bindingSet.Mode);

                if (_reusableBindingSetsByControllerFamily[bindingSet.ControllerFamily].Count is 0)
                {
                    _reusableBindingSetsByControllerFamily.Remove(bindingSet.ControllerFamily);
                }
            }

            foreach (var deviceInfo in _profileDeviceInfo.Values)
            {
                deviceInfo.OnBindingSetDeleted(bindingSet);
            }
        }

        public static void ReleaseMicrophones()
        {
            foreach (var player in PlayerContainer.Players)
            {
                player.DeviceInfo.ReleaseMicrophones();
            }
        }

        public static void ResolveMicrophones()
        {
            foreach (var player in PlayerContainer.Players)
            {
                player.DeviceInfo.ResolveMicrophones();
            }
        }

        public static string GetNameWithNumber(string name, ControllerFamily family, GameMode mode)
        {
            var existing = GetBindingSetsForControllerInMode(family, mode);

            if (existing.All(existingBindingSet => existingBindingSet.Name != name))
            {
                return name;
            }

            for (var number = 2; true; number++)
            {
                var candidateName = $"{name} #{number}";

                if (existing.All(existingBindingSet => existingBindingSet.Name != candidateName))
                {
                    return candidateName;
                }
            }
        }
    }
}
