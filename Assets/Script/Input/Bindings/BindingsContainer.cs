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
            _bindings.Clear();

            string bindingsPath = BindingsPath;
            if (!File.Exists(bindingsPath))
                return 0;

            var bindings = BindingSerialization.DeserializeBindings(bindingsPath);
            if (bindings is null)
            {
                YargLogger.LogWarning("Failed to load bindings!");
                return 0;
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

            return _bindings.Count;
        }

        public static int SaveBindings()
        {
            var serialized = new SerializedBindings();
            foreach (var (id, binds) in _bindings)
            {
                var profile = PlayerContainer.GetProfileById(id);
                if (profile is null || profile.IsBot) // Don't save bindings for bots
                    continue;

                serialized.Profiles[id] = binds.Serialize();
            }

            BindingSerialization.SerializeBindings(serialized, BindingsPath);
            return _bindings.Count;
        }
    }
}