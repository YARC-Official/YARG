using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;

namespace YARG.Themes
{
    public class ThemeManager : MonoSingleton<ThemeManager>
    {
        public const string NOTE_PREFAB_NAME = "note";

        public const string FRET_PREFAB_NAME = "fret";
        public const string KICK_FRET_PREFAB_NAME = "kickFret";

        public const string WHITE_KEY_PREFAB_NAME = "whiteKey";
        public const string BLACK_KEY_PREFAB_NAME = "blackKey";

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
            if (container is null)
            {
                return null;
            }

            var prefabKey = (gameMode, NOTE_PREFAB_NAME);

            // Try to get and return a cached version, otherwise we'll have to create it
            var cached = container.PrefabCache.GetValueOrDefault(prefabKey);
            if (cached != null)
            {
                return cached;
            }

            // Duplicate the prefab
            var gameObject = Instantiate(noModelPrefab, transform);
            var prefabCreator = gameObject.GetComponent<IThemeNoteCreator>();

            // Set the models
            var themeComp = container.GetThemeComponent();
            prefabCreator.SetThemeModels(
                themeComp.GetNoteModelsForGameMode(gameMode, false),
                themeComp.GetNoteModelsForGameMode(gameMode, true));

            // Disable and return
            gameObject.SetActive(false);
            container.PrefabCache[prefabKey] = gameObject;
            return gameObject;
        }

        public GameObject CreateFretPrefabFromTheme(ThemePreset preset, GameMode gameMode,
            string name = FRET_PREFAB_NAME)
        {
            return CreatePrefabFromTheme<ThemeFret, Fret>(preset, gameMode, name);
        }

        public GameObject CreateKickFretPrefabFromTheme(ThemePreset preset, GameMode gameMode)
        {
            return CreatePrefabFromTheme<ThemeKickFret, KickFret>(preset, gameMode, KICK_FRET_PREFAB_NAME);
        }

        public GameObject CreatePrefabFromTheme<TTheme, TBind>(ThemePreset preset, GameMode gameMode, string name)
            where TBind : MonoBehaviour, IThemeBindable<TTheme>
        {
            // Get the theme container
            var container = GetThemeContainer(preset, gameMode);
            if (container is null)
            {
                return null;
            }

            var prefabKey = (gameMode, name);

            // Try to get and return a cached version, otherwise we'll have to create it
            var cached = container.PrefabCache.GetValueOrDefault(prefabKey);
            if (cached != null)
            {
                return cached;
            }

            // Duplicate the prefab
            var prefab = container.GetThemeComponent().GetModelForGameMode(gameMode, name);
            var gameObject = Instantiate(prefab, transform);

            // Set info
            var bindComp = gameObject.AddComponent<TBind>();
            bindComp.ThemeBind = gameObject.GetComponent<TTheme>();

            // Disable and return
            gameObject.SetActive(false);
            container.PrefabCache[prefabKey] = gameObject;
            return gameObject;
        }

        public ThemeContainer GetThemeContainer(ThemePreset preset, GameMode mode)
        {
            // Check if the theme supports the game mode
            if (!preset.SupportedGameModes.Contains(mode))
            {
                YargLogger.LogFormatInfo("Theme `{0}` does not support `{1}`. Falling back to the default theme.",
                    preset.Name, mode);
                preset = ThemePreset.Default;
            }

            // Get the theme container
            var container = _themeContainers.GetValueOrDefault(preset);
            if (container is null)
            {
                YargLogger.LogFormatWarning("Could not find theme with ID `{0}`!", preset.Id);
                return null;
            }

            return container;
        }
    }
}