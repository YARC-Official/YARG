using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
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

        /// <summary>
        /// Adds all of the presets to the specified dropdown.
        /// </summary>
        /// <returns>
        /// A list containing all of the base presets in order as shown in the dropdown.
        /// </returns>
        public List<BasePreset> AddOptionsToDropdown(TMP_Dropdown dropdown)
        {
            var list = new List<BasePreset>();

            dropdown.options.Clear();

            // Add defaults
            foreach (var preset in DefaultBasePresets)
            {
                dropdown.options.Add(new($"<color=#1CCFFF>{preset.Name}</color>"));
                list.Add(preset);
            }

            // Add customs
            foreach (var preset in CustomBasePresets)
            {
                dropdown.options.Add(new(preset.Name));
                list.Add(preset);
            }

            return list;
        }

        protected static string CreateFileNameForPreset(BasePreset preset)
        {
            // Limit the file name to 20 characters
            string fileName = preset.Name;
            if (fileName.Length > 20)
            {
                fileName = fileName[..20];
            }

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
                // Skip if the user already has the preset
                if (HasPresetId(preset.Id))
                {
                    return;
                }

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
            var text = JsonConvert.SerializeObject(preset);
            var path = CreateFileNameForPreset(preset);

            File.WriteAllText(Path.Join(ContentDirectory, path), text);
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

        public T GetPresetById(Guid guid)
        {
            foreach (var preset in DefaultPresets)
            {
                if (preset.Id == guid) return preset;
            }

            foreach (var preset in CustomPresets)
            {
                if (preset.Id == guid) return preset;
            }

            return null;
        }

        public bool HasPresetId(Guid guid)
        {
            return GetPresetById(guid) is not null;
        }
    }
}