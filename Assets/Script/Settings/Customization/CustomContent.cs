using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent
    {
        private static readonly Regex _fileNameSanitize = new("([^a-zA-Z0-9 ])", RegexOptions.Compiled);

        protected readonly string ContentDirectory;

        public abstract IReadOnlyList<BasePreset> DefaultBasePresets { get; }
        public abstract IReadOnlyList<BasePreset> CustomBasePresets { get; }

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);
            ContentDirectory = contentDirectory;
        }

        public abstract void AddPreset(BasePreset preset);
        public abstract void DeletePreset(BasePreset preset);

        public abstract void SetSettingsFromPreset(BasePreset preset);
        public abstract void SetPresetFromSettings(BasePreset preset);

        protected static string CreateFileNameForPreset(BasePreset preset)
        {
            // Limit the file name to 20 characters
            string fileName = preset.Name[..20];

            // Remove symbols
            fileName = _fileNameSanitize.Replace(fileName, "_");

            // Add the end
            fileName += $".{preset.Id.ToString()[..8]}.json";

            return fileName;
        }
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

        public override void DeletePreset(BasePreset preset)
        {
            if (preset is T t)
            {
                Content.Remove(t);
                DeletePresetFile(t);
            }
            else
            {
                throw new InvalidOperationException("Attempted to add invalid preset type.");
            }
        }

        public void LoadFiles()
        {
            var guids = new HashSet<Guid>();

            Content.Clear();

            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var preset = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));

                // See if file already exists
                if (guids.Contains(preset.Id))
                {
                    Debug.LogWarning($"Duplicate preset `{path}` found!");
                    return true;
                }

                // Otherwise, add the preset
                guids.Add(preset.Id);
                Content.Add(preset);

                return true;
            });
        }

        private void SavePresetFile(T preset)
        {
            var camera = JsonConvert.SerializeObject(preset);
            var path = CreateFileNameForPreset(preset);

            File.WriteAllText(path, camera);
        }

        private void DeletePresetFile(T preset)
        {
            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var file = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));

                if (file.Id == preset.Id)
                {
                    File.Delete(path);
                    return false;
                }

                return true;
            });
        }

        public void SaveAll()
        {
            foreach (var preset in CustomPresets)
            {
                SavePresetFile(preset);
            }
        }
    }
}