using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public abstract class CustomContent
    {
        private static readonly Regex _fileNameSanitize = new("([^a-zA-Z0-9 ])", RegexOptions.Compiled);

        protected static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new JsonColorConverter()
            }
        };

        public readonly string ContentDirectory;

        public abstract IReadOnlyList<BasePreset> DefaultBasePresets { get; }
        public abstract IReadOnlyList<BasePreset> CustomBasePresets { get; }

        protected CustomContent(string contentDirectory)
        {
            Directory.CreateDirectory(contentDirectory);
            ContentDirectory = contentDirectory;
        }

        public abstract void AddPreset(BasePreset preset);
        public abstract void DeletePreset(BasePreset preset);
        public abstract void RenamePreset(BasePreset preset, string name);

        public abstract void ReloadPresetAtPath(string path);

        public abstract void SetSettingsFromPreset(BasePreset preset);
        public abstract void SetPresetFromSettings(BasePreset preset);

        public abstract void SaveAll();

        public abstract BasePreset GetBasePresetById(Guid guid);
        public abstract bool HasPresetId(Guid guid);

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

        public override void RenamePreset(BasePreset preset, string name)
        {
            if (preset is T t)
            {
                DeletePresetFile(t);
                t.Name = name;
            }
            else
            {
                throw new InvalidOperationException("Attempted to add invalid preset type.");
            }
        }

        public override void ReloadPresetAtPath(string path)
        {
            var preset = LoadFile(path);

            var loadedPreset = GetPresetById(preset.Id);

            if (loadedPreset is null)
            {
                // Just add the preset if it doesn't exist
                Content.Add(preset);
            }
            else
            {
                // Otherwise, reload it by removing it and re-adding it
                int index = Content.IndexOf(loadedPreset);
                Content.RemoveAt(index);
                Content.Insert(index, preset);
            }
        }

        public void LoadFiles()
        {
            Content.Clear();

            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var preset = LoadFile(path);

                // See if file already exists
                if (HasPresetId(preset.Id))
                {
                    Debug.LogWarning($"Duplicate preset `{path}` found!");
                }

                // Otherwise, add the preset
                Content.Add(preset);

                return true;
            });
        }

        private void SavePresetFile(T preset)
        {
            var text = JsonConvert.SerializeObject(preset, JsonSettings);
            var path = Path.Join(ContentDirectory, CreateFileNameForPreset(preset));

            File.WriteAllText(path, text);
        }

        private void DeletePresetFile(T preset)
        {
            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var file = JsonConvert.DeserializeObject<T>(File.ReadAllText(path), JsonSettings);

                if (file.Id == preset.Id)
                {
                    File.Delete(path);
                    return false;
                }

                return true;
            });
        }

        public override void SaveAll()
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

        public override BasePreset GetBasePresetById(Guid guid)
        {
            return GetPresetById(guid);
        }

        public override bool HasPresetId(Guid guid)
        {
            return GetPresetById(guid) is not null;
        }

        private static T LoadFile(string path)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), JsonSettings);
        }
    }
}