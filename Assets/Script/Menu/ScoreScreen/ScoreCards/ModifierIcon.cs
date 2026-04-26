using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Menu.ScoreScreen
{
    public class ModifierIcon : MonoBehaviour
    {
        private const string GHOSTING              = "Ghosting";
        private const string INFINITE_FRONT_END    = "InfiniteFrontEnd";
        private const string DYNAMIC_HIT_WINDOW    = "DynamicHitWindow";
        private const string SOLO_TAPS             = "SoloTaps";
        private const string NO_STAR_POWER_OVERLAP = "NoStarPowerOverlap";

        [SerializeField]
        private Image _icon;

        public void InitializeForModifier(Modifier modifier)
        {
            InitializeCustom(modifier.ToString());
        }

        public void InitializeCustom(string id)
        {
            // TODO: Try catch doesn't work
            _icon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"ModifierIcons[{id}]")
                .WaitForCompletion();
        }

        public static void SpawnEnginePresetIcons(ModifierIcon prefab, Transform parent,
            EnginePreset enginePreset, GameMode gameMode)
        {
            switch (gameMode)
            {
                case GameMode.FiveFretGuitar:
                {
                    var guitarPreset = enginePreset.FiveFretGuitar;
                    // Ghosting Icon
                    if (!guitarPreset.AntiGhosting)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(GHOSTING);
                    }

                    // Infinite Front-End Icon
                    if (guitarPreset.InfiniteFrontEnd)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(INFINITE_FRONT_END);
                    }

                    // Dynamic Hit Window
                    if (guitarPreset.HitWindow.IsDynamic)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(DYNAMIC_HIT_WINDOW);
                    }

                    // Solo Taps
                    if (guitarPreset.SoloTaps)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(SOLO_TAPS);
                    }

                    // No Star Power Overlap
                    if (guitarPreset.NoStarPowerOverlap)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(NO_STAR_POWER_OVERLAP);
                    }

                    break;
                }
                case GameMode.SixFretGuitar:
                {
                    var guitarPreset = enginePreset.SixFretGuitar;
                    // Ghosting Icon
                    if (!guitarPreset.AntiGhosting)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(GHOSTING);
                    }

                    // Infinite Front-End Icon
                    if (guitarPreset.InfiniteFrontEnd)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(INFINITE_FRONT_END);
                    }

                    // Dynamic Hit Window
                    if (guitarPreset.HitWindow.IsDynamic)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(DYNAMIC_HIT_WINDOW);
                    }

                    // Solo Taps
                    if (guitarPreset.SoloTaps)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(SOLO_TAPS);
                    }

                    // No Star Power Overlap
                    if (guitarPreset.NoStarPowerOverlap)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(NO_STAR_POWER_OVERLAP);
                    }

                    break;
                }
                case GameMode.FiveLaneDrums:
                case GameMode.FourLaneDrums:
                    // Dynamic Hit Window
                    if (enginePreset.Drums.HitWindow.IsDynamic)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(DYNAMIC_HIT_WINDOW);
                    }

                    // No Star Power Overlap
                    if (enginePreset.FiveFretGuitar.NoStarPowerOverlap)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(NO_STAR_POWER_OVERLAP);
                    }

                    break;
                case GameMode.ProKeys:
                    // No Star Power Overlap
                    if (enginePreset.FiveFretGuitar.NoStarPowerOverlap)
                    {
                        var icon = Instantiate(prefab, parent);
                        icon.InitializeCustom(NO_STAR_POWER_OVERLAP);
                    }

                    break;
            }
        }
    }
}