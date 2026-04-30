using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using static YARG.Themes.ThemeManager;
using Object = UnityEngine.Object;

namespace YARG.Themes
{
    public class ThemeContainer : IDisposable
    {
        private readonly GameObject _prefab;
        private readonly bool _builtIn;
        private readonly AssetBundle _bundle;

        public readonly Dictionary<(VisualStyle style, string Name), GameObject> PrefabCache = new();

        public ThemeContainer(GameObject themePrefab, bool builtIn, AssetBundle bundle = null)
        {
            _prefab = themePrefab;
            _builtIn = builtIn;
            _bundle = bundle;
        }

        public ThemeComponent GetThemeComponent()
        {
            return _prefab.GetComponent<ThemeComponent>();
        }

        public void Dispose()
        {
            // Destroyed the pre-created prefabs

            foreach (var (_, prefab) in PrefabCache)
            {
                Object.Destroy(prefab);
            }

            if (_builtIn) return;

            _bundle?.Unload(true);
        }
    }
}