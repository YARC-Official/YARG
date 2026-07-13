using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
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
            // Create directory but do NOT call base.Initialize() - that loads JSON files
            // Theme presets are loaded from .yargtheme bundles, not JSON files
            Directory.CreateDirectory(FullContentDirectory);

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
            // Load bundle to read embedded metadata
            var bundle = AssetBundle.LoadFromFile(file);
            if (bundle == null)
            {
                YargLogger.LogFormatWarning("Failed to load bundle '{0}', skipping.", file);
                return;
            }

            // Read embedded metadata TextAsset
            var metaText = bundle.LoadAsset<TextAsset>(ThemeComponent.THEME_META_NAME);
            if (metaText == null)
            {
                bundle.Unload(false);
                YargLogger.LogFormatWarning("Theme '{0}' missing theme_meta, skipping.", file);
                return;
            }

            var preset = JsonConvert.DeserializeObject<ThemePreset>(metaText.text, JsonSettings);
            preset.CustomBundlePath = file; // .yargtheme IS the bundle
            preset.Path = file;

            // Unload temp bundle — will be re-loaded at gameplay time by CreateThemeContainer
            bundle.Unload(false);

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
