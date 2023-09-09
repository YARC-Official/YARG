using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent
    {
        public readonly string ContentDirectory;

        public abstract IReadOnlyList<BasePreset> DefaultBasePresets { get; }
        public abstract IReadOnlyList<BasePreset> CustomBasePresets { get; }

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);
            ContentDirectory = contentDirectory;
        }

        public abstract void AddPreset(BasePreset preset);

        public abstract void SetSettingsFromPreset(BasePreset preset);
        public abstract void SetPresetFromSettings(BasePreset preset);
    }

    public abstract class CustomContent<T> : CustomContent where T : BasePreset
    {
        protected readonly List<T> Content = new();

        public abstract IReadOnlyList<T> DefaultPresets { get; }
        public override IReadOnlyList<BasePreset> DefaultBasePresets => DefaultPresets;

        public IReadOnlyList<T> CustomPresets => Content;
        public override IReadOnlyList<BasePreset> CustomBasePresets => CustomPresets;

        protected CustomContent(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void AddPreset(BasePreset preset)
        {
            if (preset is T t)
            {
                Content.Add(t);
            }
            else
            {
                throw new InvalidOperationException("Attempted to add invalid preset type.");
            }
        }

        public abstract void LoadFiles();

        public void SaveFiles()
        {
            foreach (var item in CustomPresets)
            {
                SaveItem(item);
            }
        }

        public abstract void SaveItem(T item);
    }
}