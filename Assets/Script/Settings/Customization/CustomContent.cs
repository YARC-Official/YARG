using System.Collections.Generic;
using System.IO;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent<T>
    {

        public readonly string ContentDirectory;

        public readonly Dictionary<string, T> Content;

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);

            ContentDirectory = contentDirectory;
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