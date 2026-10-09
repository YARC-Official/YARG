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

        // The shared tom/cymbal hit light's hold time and intensity are tuned in the theme
        // prefab. Kick's cloned copy needs its own override (rather than inheriting the
        // clone's resolved value) because it must stay in sync with that tuning independently
        // of code changes elsewhere. Keep these equal to the tom/cymbal hold time/intensity
        // set on the Drums fret's EffectLight/Light override in RectangularTheme.prefab.
        private const float KICK_HIT_LIGHT_FADE_OUT_RATE = 90f;
        private const float KICK_HIT_LIGHT_INTENSITY = 6f;

        // Same idea as above, but for the miss light: keep in sync with the tom/cymbal Miss
        // Effects light's hold time/range in RectangularTheme.prefab, then push intensity up
        // slightly beyond that baseline so a kick miss still reads clearly on its own (it has
        // no flare/sparkle particles to help it stand out).
        private const float KICK_MISS_LIGHT_FADE_OUT_RATE = 40f;
        private const float KICK_MISS_LIGHT_RANGE = 0.25f;
        private const float KICK_MISS_LIGHT_INTENSITY = 2.9f;

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [field: SerializeField]
        [field: HideInInspector]
        public ThemeKickFret ThemeBind { get; set; }

        // Runtime clone of ThemeBind.HitEffect, parented under this fret so the
        // same particles/lights used for tom/cymbal hits also play on kick hits.
        private EffectGroup _hitEffect;

        // Runtime clone of ThemeBind.MissEffect, used instead of _hitEffect when a kick input
        // misses so it doesn't show the hit effect's flare/sparkle particles.
        private EffectGroup _missEffect;

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
                    light.SetIntensity(KICK_HIT_LIGHT_INTENSITY);
                }

                // Must run before ApplyDrumHitEffectTuning (below), which boosts the particle
                // colors into HDR range - if SetColor ran after, it would overwrite that boost
                // with the plain (non-boosted) tint.
                _hitEffect.SetColor(particleColor.ToUnityColor());
                Fret.ApplyDrumHitEffectTuning(_hitEffect);
            }

            if (_missEffect == null && ThemeBind.MissEffect != null)
            {
                _missEffect = Instantiate(ThemeBind.MissEffect, transform);
                foreach (var light in _missEffect.EffectLights)
                {
                    light.SetFadeOutRate(KICK_MISS_LIGHT_FADE_OUT_RATE);
                    light.SetRange(KICK_MISS_LIGHT_RANGE);
                    light.SetIntensity(KICK_MISS_LIGHT_INTENSITY);
                }

                // The cloned Miss Effects group still has its Smoke/Dark Smoke particles
                // enabled (those are only disabled via a drums-specific prefab override that
                // this direct clone bypasses) - disable them so a kick miss is just a light
                // flash, with no flare or sparkles.
                foreach (var particle in _missEffect.EffectParticles)
                {
                    if (particle.name is "Smoke" or "Dark Smoke")
                    {
                        particle.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void PlayHitAnimation()
        {
            ThemeBind.Animator.SetTrigger(_hit);
            _hitEffect?.Play();
        }

        public void PlayMissAnimation()
        {
            ThemeBind.Animator.SetTrigger(_hit);
            _missEffect?.Play();
        }

        public static void CreateFromThemeKickFret(ThemeKickFret themeKickFret)
        {
            var fretComp = themeKickFret.gameObject.AddComponent<KickFret>();
            fretComp.ThemeBind = themeKickFret;
        }
    }
}