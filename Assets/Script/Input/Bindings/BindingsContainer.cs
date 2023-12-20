using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Game;
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
            if (!File.Exists(bindingsPath)) return 0;

            string bindingsJson = File.ReadAllText(bindingsPath);
            Dictionary<Guid, SerializedProfileBindings> bindings;
            try
            {
                bindings = JsonConvert.DeserializeObject<Dictionary<Guid, SerializedProfileBindings>>(bindingsJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while loading bindings!");
                Debug.LogException(ex);
                return 0;
            }

            if (bindings is null)
            {
                Debug.LogWarning($"Failed to load bindings!");
                return 0;
            }

            foreach (var (id, serialized) in bindings)
            {
                var profile = PlayerContainer.GetProfileById(id);
                if (profile is null)
                {
                    Debug.LogWarning($"Bindings exist for profile ID {id}, but the corresponding profile was not found! Bindings will be discarded.");
                    continue;
                }

                var deserialized = ProfileBindings.Deserialize(profile, serialized);
                _bindings.Add(id, deserialized);
            }

            return _bindings.Count;
        }

        public static int SaveBindings()
        {
            string bindingsJson = JsonConvert.SerializeObject(_bindings, Formatting.Indented);
            File.WriteAllText(BindingsPath, bindingsJson);

            return _bindings.Count;
        }
    }
}