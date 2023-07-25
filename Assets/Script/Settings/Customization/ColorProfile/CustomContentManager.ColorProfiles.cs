using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Settings.Customization
{
    public static partial class CustomContentManager
    {
        public static class ColorProfiles
        {
            private const string COLORS_FOLDER = "colorProfiles";
            private static string ColorProfilesDirectory => Path.Combine(CustomizationDirectory, COLORS_FOLDER);

            private static readonly Dictionary<string, ColorProfile> _profiles = new();
            public static IReadOnlyDictionary<string, ColorProfile> Profiles => _profiles;

            public static void Add(string name, ColorProfile profile)
            {
                if (_profiles.ContainsKey(name))
                    throw new ArgumentException($"A color profile already exists under the name of {name}!", nameof(name));

                _profiles.Add(name, profile);
            }

            public static void Remove(string name)
            {
                _profiles.Remove(name);
            }

            public static void Load() => LoadFiles(_profiles, ColorProfilesDirectory);

            public static void Save(ColorProfile profile)
            {
                SaveItem(profile, profile.Name, ColorProfilesDirectory);
            }
        }
    }
}