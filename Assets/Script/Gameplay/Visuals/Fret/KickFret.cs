using UnityEngine;
using YARG.Helpers.Authoring;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class KickFret : MonoBehaviour, IThemeBindable<ThemeKickFret>
    {
        private static readonly int _hit = Animator.StringToHash("Hit");

        // The shared tom/cymbal hit light was tuned to be near-instant (almost no hold time).
        // Kick's cloned copy keeps its own, more visible hold time so its light isn't lost
        // next to the purple fret pieces.
        private const float KICK_HIT_LIGHT_FADE_OUT_RATE = 48f;

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [field: SerializeField]
        [field: HideInInspector]
        public ThemeKickFret ThemeBind { get; set; }

        // Runtime clone of ThemeBind.HitEffect, parented under this fret so the
        // same particles/lights used for tom/cymbal hits also play on kick hits.
        private EffectGroup _hitEffect;

        public void Initialize(Color color, Color particleColor)
        {
            foreach (var material in ThemeBind.GetColoredMaterials())
            {
                material.color = color.ToUnityColor();
            }

            if (_hitEffect == null && ThemeBind.HitEffect != null)
            {
                _hitEffect = Instantiate(ThemeBind.HitEffect, transform);
                foreach (var light in _hitEffect.EffectLights)
                {
                    light.SetFadeOutRate(KICK_HIT_LIGHT_FADE_OUT_RATE);
                }

                Fret.ApplyDrumHitEffectTuning(_hitEffect);
            }

            _hitEffect?.SetColor(particleColor.ToUnityColor());
        }

        public void PlayHitAnimation()
        {
            ThemeBind.Animator.SetTrigger(_hit);
            _hitEffect?.Play();
        }

        public static void CreateFromThemeKickFret(ThemeKickFret themeKickFret)
        {
            var fretComp = themeKickFret.gameObject.AddComponent<KickFret>();
            fretComp.ThemeBind = themeKickFret;
        }
    }
}