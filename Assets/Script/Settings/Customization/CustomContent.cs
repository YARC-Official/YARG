using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent<T>
    {
        public readonly string ContentDirectory;
        public readonly Dictionary<Guid, T> Content;

        public abstract T Default { get; }

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);

            ContentDirectory = contentDirectory;
            Content = new();
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

        public T GetContentOrDefault(Guid id)
        {
            if (Content.TryGetValue(id, out var content))
            {
                return content;
            }

            return Default;
        }
    }
}