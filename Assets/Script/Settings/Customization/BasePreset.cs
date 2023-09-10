using System;
using Newtonsoft.Json;

namespace YARG.Settings.Customization
{
    public abstract class BasePreset
    {
        public string Name;
        public Guid Id;

        /// <summary>
        /// Determines whether or not the preset should be modifiable in the settings.
        /// </summary>
        [JsonIgnore]
        public bool DefaultPreset;

        protected BasePreset(string name, bool defaultPreset)
        {
            Name = name;
            Id = Guid.NewGuid();
            DefaultPreset = defaultPreset;
        }

        public abstract BasePreset CopyWithNewName(string name);
    }
}