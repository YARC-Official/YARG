using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Themes
{
    public partial class ThemePreset : BasePreset
    {
        public string AssetBundleThemePath;

        public List<VisualStyle> SupportedStyles = new();

        public Guid PreferredColorProfile = Guid.Empty;
        public Guid PreferredCameraPreset = Guid.Empty;

        // Path to custom bundle file (null for built-in themes)
        public string CustomBundlePath;

        // Format version for forward compatibility (defaults to 1)
        public int FormatVersion = 1;

        public ThemePreset(string name, bool defaultPreset)
            : base(name, defaultPreset)
        {
        }

        public ThemeContainer CreateThemeContainer()
        {
            const int CurrentFormatVersion = 1;

            if (FormatVersion > CurrentFormatVersion)
            {
                YargLogger.LogFormatWarning(
                    "Theme '{0}' was created with format version {1}, but this game supports {2}. Loading with best-effort compatibility.",
                    Name, FormatVersion, CurrentFormatVersion);
            }

            // Custom bundle path — load from file
            if (!string.IsNullOrEmpty(CustomBundlePath) && File.Exists(CustomBundlePath))
            {
                var bundle = AssetBundle.LoadFromFile(CustomBundlePath);
                if (bundle == null)
                {
                    YargLogger.LogFormatError("Failed to load asset bundle for theme '{0}'", Name);
                    return null;
                }

                var themePrefab = bundle.LoadAsset<GameObject>(BackgroundHelper.GENERIC_PREFAB_PATH);
                if (themePrefab == null)
                {
                    bundle.Unload(true);
                    YargLogger.LogFormatError("Theme '{0}' bundle missing 'ThemeRoot' prefab", Name);
                    return null;
                }

                // Fixup Metal shaders on macOS (replaces renderer shaders with bundled Metal variants)
                BackgroundHelper.LoadMetalShaders(bundle, themePrefab, BackgroundHelper.ExportType.Generic);

                return new ThemeContainer(themePrefab, false, bundle);
            }

            // Built-in — load via Addressables
            if (DefaultPreset)
            {
                var themePrefab = Addressables
                    .LoadAssetAsync<GameObject>(AssetBundleThemePath)
                    .WaitForCompletion();

                return new ThemeContainer(themePrefab, true);
            }

            throw new NotImplementedException();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ThemePreset(name, false)
            {
                SupportedStyles     = new List<VisualStyle>(SupportedStyles),
                FormatVersion       = FormatVersion
                // CustomBundlePath intentionally NOT copied — copy is not yet on disk
            };
        }
    }
}
