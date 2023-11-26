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
            // Get the theme container
            var container = GetThemeContainer(preset, gameMode);
            if (container is null) return null;

            // Try to get and return a cached version
            var cached = container.FretCache.GetValueOrDefault(gameMode);
            if (cached != null) return cached;
            // ...otherwise we'll have to create it

            // Duplicate the prefab
            var themeFret = container.GetThemeComponent().GetFretModelForGameMode(gameMode);
            var gameObject = Instantiate(themeFret, transform);

            // Set info
            Fret.CreateFromThemeFret(gameObject.GetComponent<ThemeFret>());

            // Disable and return
            gameObject.SetActive(false);
            container.FretCache[gameMode] = gameObject;
            return gameObject;
        }

        private ThemeContainer GetThemeContainer(ThemePreset preset, GameMode mode)
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