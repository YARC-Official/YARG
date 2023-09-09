namespace YARG.Settings.Customization
{
    public abstract class BasePreset
    {
        public string Name;

        /// <summary>
        /// Determines whether or not the preset should be modifiable in the settings.
        /// </summary>
        public bool DefaultPreset;

        protected BasePreset(string name, bool defaultPreset)
        {
            Name = name;
            DefaultPreset = defaultPreset;
        }

        public abstract BasePreset CopyWithNewName(string name);
    }
}