using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input.Serialization;
using YARG.Player;

namespace YARG.Input.Bindings
{
    /// <summary>
    /// Manages all of the <see cref="ProfileBindings"/> for <see cref="YargProfile"/>/<see cref="YargPlayer"/>s.
    /// </summary>
    public static class BindingsContainer
    {
        private static string BindingsPath => Path.Combine(PlayerContainer.ProfilesDirectory, "bindings.json");
        private static string BindingsBackupPath => Path.Combine(PlayerContainer.ProfilesDirectory, "bindings.json.bak");

        private static readonly Dictionary<Guid, ProfileBindings> _bindings = new();

        public static ProfileBindings GetBindingsForProfile(YargProfile profile)
        {
            if (!_bindings.TryGetValue(profile.Id, out var bindings))
            {
                // Bindings must always be provided
                bindings = new(profile);
                _bindings.Add(profile.Id, bindings);
            }

            return bindings;
        }

        public static int LoadBindings()
        {
            bool usedBackup = false;

            _bindings.Clear();

            string bindingsPath = BindingsPath;
            if (!File.Exists(bindingsPath))
                return 0;

            var bindings = BindingSerialization.DeserializeBindings(bindingsPath);
            if (bindings is null)
            {
                YargLogger.LogWarning("Failed to load bindings! Attempting to load backup.");

                bindings = BindingSerialization.DeserializeBindings(BindingsBackupPath);
                if (bindings is null)
                {
                    YargLogger.LogWarning("Failed to load bindings from backup!");
                    return 0;
                }
                usedBackup = true;
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

                var deserialized = ProfileBindings.Deserialize(profile, serialized);
                _bindings.Add(id, deserialized);
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

            return _bindings.Count;
        }

        public static int SaveBindings(string path = null)
        {
            path ??= BindingsPath;

            var serialized = new SerializedBindings();
            foreach (var (id, binds) in _bindings)
            {
                var profile = PlayerContainer.GetProfileById(id);
                if (profile is null || profile.IsBot) // Don't save bindings for bots
                    continue;

                serialized.Profiles[id] = binds.Serialize();
            }

            BindingSerialization.SerializeBindings(serialized, path);
            return _bindings.Count;
        }
    }
}