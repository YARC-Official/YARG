using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using Object = UnityEngine.Object;

namespace YARG.Themes
{
    public class ThemeContainer : IDisposable
    {
        private readonly GameObject _prefab;
        private readonly bool _builtIn;

        public readonly Dictionary<(GameMode GameMode, string Name), GameObject> PrefabCache = new();

        public ThemeContainer(GameObject themePrefab, bool builtIn)
        {
            _prefab = themePrefab;
            _builtIn = builtIn;
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

            // TODO: Deal with asset bundle unloading
        }
    }
}