using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Content;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
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

        private static readonly Dictionary<Guid, PlayerDeviceInfo> _profileBindings = new();

        private static readonly Dictionary<Guid, ReusableBindingSet> _allBindingCollectionsByGuid = new();

        private static readonly Dictionary<ControllerFamily, List<ReusableBindingSet>> _reusableBindingSetsByControllerFamily = new()
        {
            {
                ControllerFamily.FiveFretGuitar,
                new() {
                    ReusableBindingSetDefaults.DefaultFiveFretGuitar,
                    ReusableBindingSetDefaults.DefaultRiffmasterGuitar,

                    // Menu
                    ReusableBindingSetDefaults.DefaultFiveFretGuitarMenu,
                }
            },

            {
                ControllerFamily.SixFretGuitar,
                new()
                {
                    ReusableBindingSetDefaults.DefaultSixFretGuitar,
                }
            },

            {
                ControllerFamily.FourLaneDrumkit,
                new()
                {
                    ReusableBindingSetDefaults.DefaultFourLaneDrumkit,

                    // Menu
                    ReusableBindingSetDefaults.DefaultFourLaneDrumkitMenu,
                    ReusableBindingSetDefaults.FourLaneDrumkitManualMenu,
                }
            },

            {
                ControllerFamily.FiveLaneDrumkit,
                new()
                {
                    ReusableBindingSetDefaults.DefaultFiveLaneDrumkit,
                }
            },
        };

        private static readonly Dictionary<(GameMode mode, ControllerFamily controllerFamily), List<ReusableBindingSet>> _bindingCollectionsByContext = new();

        public static PlayerDeviceInfo GetBindingsForProfile(YargProfile profile)
        {
            if (!_profileBindings.TryGetValue(profile.Id, out var bindings))
            {
                // Bindings must always be provided
                bindings = new();
                _profileBindings.Add(profile.Id, bindings);
            }

            return bindings;
        }

        public static List<ReusableBindingSet> GetBindingSetsForControllerFamily(ControllerFamily controllerFamily)
        {
            return _reusableBindingSetsByControllerFamily.GetValueOrDefault(controllerFamily, new());
        }

        public static List<ReusableBindingSet> GetBindingSetsForControllerInMode(ControllerFamily controllerFamily, GameMode? mode)
        {
            List<ReusableBindingSet> bindingSets = new();

            var forFamily = GetBindingSetsForControllerFamily(controllerFamily);

            foreach (var bindingSet in forFamily)
            {
                if (bindingSet.Mode == mode)
                {
                    bindingSets.Add(bindingSet);
                }
            }

            return bindingSets;
        }


        public static bool TryGetBindingCollectionById(Guid guid, out ReusableBindingSet bindingCollection)
        {
            if (_allBindingCollectionsByGuid.ContainsKey(guid))
            {
                bindingCollection = _allBindingCollectionsByGuid[guid];
                return true;
            }

            bindingCollection = null;
            return false;
        }

        public static void LoadBindings()
        {
            bool usedBackup = false;

            _profileBindings.Clear();

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
                var bindingCollection = new ReusableBindingSet(serializedReusableBindingSet);

                _allBindingCollectionsByGuid.Add(guid, bindingCollection);

                if (!_reusableBindingSetsByControllerFamily.ContainsKey(bindingCollection.ControllerFamily))
                {
                    _reusableBindingSetsByControllerFamily[bindingCollection.ControllerFamily] = new() { bindingCollection };
                }
                else
                {
                    _reusableBindingSetsByControllerFamily[bindingCollection.ControllerFamily].Add(bindingCollection);
                }

                var tupleKey = (bindingCollection.Mode.Value, bindingCollection.ControllerFamily);

                if (!_bindingCollectionsByContext.ContainsKey(tupleKey)) {
                    _bindingCollectionsByContext[tupleKey] = new() { bindingCollection };
                }
                else
                {
                    _bindingCollectionsByContext[tupleKey].Add(bindingCollection);
                }
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
                _profileBindings.Add(id, deserialized);
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
            foreach (var (id, binds) in _profileBindings)
            {
                var profile = PlayerContainer.GetProfileById(id);
                if (profile is null || profile.IsBot) // Don't save bindings for bots
                    continue;

                serialized.Profiles[id] = binds.Serialize();
            }

            BindingSerialization.SerializeBindings(serialized, path);
            return _profileBindings.Count;
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
    }
}
