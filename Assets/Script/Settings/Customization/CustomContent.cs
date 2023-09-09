using System.Collections.Generic;
using System.IO;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent
    {
        public readonly string ContentDirectory;

        public abstract IEnumerable<string> DefaultPresetNames { get; }
        public abstract IEnumerable<string> CustomPresetNames { get; }

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);
            ContentDirectory = contentDirectory;
        }
    }

    public abstract class CustomContent<T> : CustomContent where T : BasePreset
    {
        public readonly Dictionary<string, T> Content;

        public abstract IEnumerable<T> DefaultPresets { get; }

        public override IEnumerable<string> CustomPresetNames => Content.Keys;

        protected CustomContent(string contentDirectory) : base(contentDirectory)
        {
            Content = new Dictionary<string, T>();
        }

        public abstract void LoadFiles();

        public void SaveFiles()
        {
            foreach (var item in Content)
            {
                SaveItem(item.Value);
            }
        }

        public abstract void SaveItem(T item);
    }
}