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

        public readonly Dictionary<GameMode, GameObject> NoteCache = new();
        public readonly Dictionary<GameMode, GameObject> FretCache = new();

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

            foreach (var (_, notePrefab) in NoteCache)
            {
                Object.Destroy(notePrefab);
            }

            foreach (var (_, fretPrefab) in FretCache)
            {
                Object.Destroy(fretPrefab);
            }

            if (_builtIn) return;

            // TODO: Deal with asset bundle unloading
        }
    }
}