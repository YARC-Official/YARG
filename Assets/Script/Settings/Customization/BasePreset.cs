namespace YARG.Settings.Customization
{
    public abstract class BasePreset
    {
        public string Name;

        protected BasePreset(string name)
        {
            Name = name;
        }

        public abstract BasePreset CopyWithNewName(string name);
    }
}