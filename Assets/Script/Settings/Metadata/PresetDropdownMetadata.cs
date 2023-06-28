using System.Collections.Generic;

namespace YARG.Settings.Metadata
{
    public readonly struct DropdownPreset
    {
        public readonly string Name;

        private readonly Dictionary<string, object> _values;
        public IReadOnlyDictionary<string, object> Values => _values;

        public DropdownPreset(string name, Dictionary<string, object> values)
        {
            Name = name;
            _values = values;
        }
    }

    public class PresetDropdownMetadata : AbstractMetadata
    {
        public string DropdownName { get; private set; }
        public string[] ModifiedSettings { get; private set; }

        private readonly List<DropdownPreset> _defaultPresets;
        public IReadOnlyList<DropdownPreset> DefaultPresets => _defaultPresets;

        public PresetDropdownMetadata(string dropdownName, string[] modifiedSettings,
            List<DropdownPreset> defaultPresets)
        {
            DropdownName = dropdownName;
            ModifiedSettings = modifiedSettings;
            _defaultPresets = defaultPresets;
        }
    }
}