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

        private readonly Dictionary<GameMode, GameObject> _gameModeNoteCache = new();

        public ThemeContainer(GameObject themePrefab, bool builtIn)
        {
            _prefab = themePrefab;
            _builtIn = builtIn;
        }

        public ThemeComponent GetThemeComponent()
        {
            return _prefab.GetComponent<ThemeComponent>();
        }

        public GameObject GetCachedNotePrefab(GameMode gameMode)
        {
            return _gameModeNoteCache.GetValueOrDefault(gameMode);
        }

        public void SetCachedNotePrefab(GameMode gameMode, GameObject prefab)
        {
            _gameModeNoteCache[gameMode] = prefab;
        }

        public void Dispose()
        {
            // Destroyed the pre-created prefabs
            foreach (var (_, notePrefab) in _gameModeNoteCache)
            {
                Object.Destroy(notePrefab);
            }

            if (_builtIn) return;
        }
    }
}