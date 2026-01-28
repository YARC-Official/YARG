using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Game;
using static YARG.Themes.ThemeManager;

namespace YARG.Themes
{
    public partial class ThemePreset : BasePreset
    {
        public string AssetBundleThemePath;

        public List<VisualStyle> SupportedStyles = new();

        public Guid PreferredColorProfile = Guid.Empty;
        public Guid PreferredCameraPreset = Guid.Empty;

        public ThemePreset(string name, bool defaultPreset)
            : base(name, defaultPreset)
        {
        }

        public ThemeContainer CreateThemeContainer()
        {
            if (DefaultPreset)
            {
                var themePrefab = Addressables
                    .LoadAssetAsync<GameObject>(AssetBundleThemePath)
                    .WaitForCompletion();

                return new ThemeContainer(themePrefab, true);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ThemePreset(name, false)
            {
                SupportedStyles = new List<VisualStyle>(SupportedStyles)
            };
        }
    }
}