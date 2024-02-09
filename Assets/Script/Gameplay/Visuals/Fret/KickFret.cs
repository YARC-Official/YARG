using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class KickFret : MonoBehaviour, IThemeBindable<ThemeKickFret>
    {
        private static readonly int _hit = Animator.StringToHash("Hit");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [field: SerializeField]
        [field: HideInInspector]
        public ThemeKickFret ThemeBind { get; set; }

        public void Initialize(Color color)
        {
            foreach (var material in ThemeBind.GetColoredMaterials())
            {
                material.color = color.ToUnityColor();
            }
        }

        public void PlayHitAnimation()
        {
            ThemeBind.Animator.SetTrigger(_hit);
        }

        public static void CreateFromThemeKickFret(ThemeKickFret themeKickFret)
        {
            var fretComp = themeKickFret.gameObject.AddComponent<KickFret>();
            fretComp.ThemeBind = themeKickFret;
        }
    }
}