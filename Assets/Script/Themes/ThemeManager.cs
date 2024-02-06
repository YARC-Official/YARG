using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Gameplay.Visuals;

namespace YARG.Themes
{
    public class ThemeManager : MonoSingleton<ThemeManager>
    {
        private readonly Dictionary<ThemePreset, ThemeContainer> _themeContainers = new();

        private void Start()
        {
            // Populate all of the default themes
            foreach (var defaultPreset in ThemePreset.Defaults)
            {
                _themeContainers.Add(defaultPreset, defaultPreset.CreateThemeContainer());
            }
        }

        public GameObject CreateNotePrefabFromTheme(ThemePreset preset, GameMode gameMode, GameObject noModelPrefab)
        {
            // Get the theme container
            var container = GetThemeContainer(preset, gameMode);
            if (container is null) return null;

            // Try to get and return a cached version
            var cached = container.NoteCache.GetValueOrDefault(gameMode);
            if (cached != null) return cached;
            // ...otherwise we'll have to create it

            // Duplicate the prefab
            var gameObject = Instantiate(noModelPrefab, transform);
            var prefabCreator = gameObject.GetComponent<IThemePrefabCreator>();

            // Set the models
            var themeComp = container.GetThemeComponent();
            prefabCreator.SetThemeModels(
                themeComp.GetNoteModelsForGameMode(gameMode, false),
                themeComp.GetNoteModelsForGameMode(gameMode, true));

            // Disable and return
            gameObject.SetActive(false);
            container.NoteCache[gameMode] = gameObject;
            return gameObject;
        }

        public GameObject CreateFretPrefabFromTheme(ThemePreset preset, GameMode gameMode)
        {
            return CreatePrefabFromTheme<ThemeFret, Fret>(preset, gameMode);
        }

        public GameObject CreateKickFretPrefabFromTheme(ThemePreset preset, GameMode gameMode)
        {
            return CreatePrefabFromTheme<ThemeKickFret, KickFret>(preset, gameMode);
        }

        private GameObject CreatePrefabFromTheme<TTheme, TBind>(ThemePreset preset, GameMode gameMode)
            where TBind : MonoBehaviour, IThemeBindable<TTheme>
        {
            // Get the theme container
            var container = GetThemeContainer(preset, gameMode);
            if (container is null)
            {
                return null;
            }

            // Try to get the prefab cache
            Dictionary<GameMode, GameObject> prefabCache;
            if (container.PrefabCache.TryGetValue(typeof(TTheme), out var cache))
            {
                prefabCache = cache;
            }
            else
            {
                prefabCache = new Dictionary<GameMode, GameObject>();
                container.PrefabCache[typeof(TTheme)] = prefabCache;
            }

            // Try to get and return a cached version, otherwise we'll have to create it
            var cached = prefabCache.GetValueOrDefault(gameMode);
            if (cached != null)
            {
                return cached;
            }

            // Duplicate the prefab
            var prefab = container.GetThemeComponent().GetModelForGameMode<TTheme>(gameMode);
            var gameObject = Instantiate(prefab, transform);

            // Set info
            var bindComp = gameObject.AddComponent<TBind>();
            bindComp.ThemeBind = gameObject.GetComponent<TTheme>();

            // Disable and return
            gameObject.SetActive(false);
            prefabCache[gameMode] = gameObject;
            return gameObject;
        }

        public ThemeContainer GetThemeContainer(ThemePreset preset, GameMode mode)
        {
            // Check if the theme supports the game mode
            if (!preset.SupportedGameModes.Contains(mode))
            {
                Debug.Log($"Theme `{preset.Name}` does not support `{mode}`. Falling back to the default theme.");
                preset = ThemePreset.Default;
            }

            // Get the theme container
            var container = _themeContainers.GetValueOrDefault(preset);
            if (container is null)
            {
                Debug.LogWarning($"Could not find theme with ID `{preset.Id}`!");
                return null;
            }

            return container;
        }
    }
}