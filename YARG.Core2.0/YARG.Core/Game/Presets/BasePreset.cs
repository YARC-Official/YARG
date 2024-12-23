using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace YARG.Core.Game
{
    public abstract class BasePreset
    {
        /// <summary>
        /// The display name of the preset.
        /// </summary>
        public string Name;

        /// <summary>
        /// The unique ID of the preset.
        /// </summary>
        public Guid Id;

        /// <summary>
        /// The type of the preset in string form. This is only
        /// used for checking the type when importing a preset.
        /// </summary>
        public string? Type;

        /// <summary>
        /// Determines whether or not the preset should be modifiable in the settings.
        /// </summary>
        [JsonIgnore]
        public bool DefaultPreset;

        /// <summary>
        /// The path of the preset. This is only used to determine the path when it's in class form.
        /// </summary>
        [JsonIgnore]
        public string? Path;

        protected BasePreset(string name, bool defaultPreset)
        {
            Name = name;
            DefaultPreset = defaultPreset;

            Id = defaultPreset
                ? GetGuidForBasePreset(name)
                : Guid.NewGuid();
        }

        public abstract BasePreset CopyWithNewName(string name);

        private static Guid GetGuidForBasePreset(string name)
        {
            // Make sure default presets are consistent based on names.
            // This ensures that their GUIDs will be consistent (because they are constructed in code every time).
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            return new Guid(hash);
        }
    }
}