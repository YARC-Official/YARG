using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;

namespace YARG.Themes
{
    public partial class ThemePreset : BasePreset
    {
        public string AssetBundleThemePath;

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
            return new ThemePreset(name, false);
        }
    }
}