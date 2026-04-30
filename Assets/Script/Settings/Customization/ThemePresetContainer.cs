using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Themes;

namespace YARG.Settings.Customization
{
    public class ThemePresetContainer : CustomContent<ThemePreset>
    {
        protected override string ContentDirectory => "themes";

        public override string PresetTypeStringName => "ThemePreset";

        public override IReadOnlyList<ThemePreset> DefaultPresets => ThemePreset.Defaults;

        public override void Initialize()
        {
            base.Initialize(); // loads JSON presets from custom/themes/

            if (!Directory.Exists(FullContentDirectory)) return;

            string[] themeFiles = Directory.GetFiles(FullContentDirectory, "*.yargtheme");
            foreach (string file in themeFiles)
            {
                try
                {
                    LoadThemeFile(file);
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Failed to load theme '{file}'");
                }
            }
        }

        private void LoadThemeFile(string file)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string extractDir = Path.Combine(FullContentDirectory, fileName);

            // Extract if not already extracted, or if zip is newer
            bool needsExtract = !Directory.Exists(extractDir);
            if (!needsExtract)
            {
                try
                {
                    needsExtract = File.GetLastWriteTime(file) > Directory.GetLastWriteTime(extractDir);
                }
                catch { needsExtract = true; }
            }

            if (needsExtract)
            {
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(file, extractDir);
            }

            // Read theme.json
            string jsonPath = Path.Combine(extractDir, "theme.json");
            if (!File.Exists(jsonPath))
            {
                YargLogger.LogFormatWarning("Theme '{0}' missing theme.json, skipping.", file);
                return;
            }

            var preset = JsonConvert.DeserializeObject<ThemePreset>(
                File.ReadAllText(jsonPath), JsonSettings);

            // Resolve bundle path
            preset.CustomBundlePath = Path.Combine(extractDir, "theme.bundle");
            preset.Path = file;

            // Skip duplicates
            if (HasPresetId(preset.Id))
            {
                YargLogger.LogFormatWarning("Duplicate theme '{0}' (ID {1}), skipping.", preset.Name, preset.Id);
                return;
            }

            Content.Add(preset);
        }
    }
}