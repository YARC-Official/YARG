using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers.Authoring;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public class Fret : MonoBehaviour, IThemeBindable<ThemeFret>
    {
        private static readonly int _fade          = Shader.PropertyToID("Fade");
        private static readonly int _secondaryFade = Shader.PropertyToID("SecondaryFade");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _secondaryColor = Shader.PropertyToID("_SecondaryColor");
        private static readonly int _secondaryEmissionColor = Shader.PropertyToID("_SecondaryEmissionColor");

        private static readonly int _hit       = Animator.StringToHash("Hit");
        private static readonly int _cymbalHit = Animator.StringToHash("CymbalHit");
        private static readonly int _miss      = Animator.StringToHash("Miss");
        private static readonly int _openMiss  = Animator.StringToHash("OpenMiss");
        private static readonly int _pressed   = Animator.StringToHash("Pressed");
        private static readonly int _sustain   = Animator.StringToHash("Sustain");
        private static readonly int _secondaryPressed = Animator.StringToHash("SecondaryPressed");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [field: SerializeField]
        [field: HideInInspector]
        public ThemeFret ThemeBind { get; set; }

        private readonly List<Material> _topMaterials   = new();
        private readonly List<Material> _innerMaterials = new();

        // Secondary half materials (for 6-fret dual-half frets)
        private readonly List<Material> _secondaryTopMaterials   = new();
        private readonly List<Material> _secondaryInnerMaterials = new();

        private bool _hasPressedParam;
        private bool _hasSustainParam;
        private bool _hasOpenMissTrigger;
        private bool _hasSecondaryPressedParam;

        // These need to be saved since the colors can now change during play
        // They are saved as Unity colors to avoid having to repeatedly convert
        // when transitioning between active and inactive states
        private UnityEngine.Color _originalUnityTopColor;
        private UnityEngine.Color _originalUnityInnerColor;
        private UnityEngine.Color _originalEmissionColor;

        // Secondary half original colors
        private UnityEngine.Color _secondaryOriginalUnityTopColor;
        private UnityEngine.Color _secondaryOriginalUnityInnerColor;
        private UnityEngine.Color _secondaryOriginalEmissionColor;

        // TODO: Consider making this customizable or perhaps just a desaturated and dimmed version of the base color
        private UnityEngine.Color _inactiveColor = new(0.321f, 0.321f, 0.321f, 1.0f);

        private bool _active             = true;
        private bool _colorChangeEnabled = false;
        private bool _fadeDirection      = true;
        // True is pulsing, false is fading
        private bool  _pulseOrFade  = true;
        private float _fadeDuration = 0.25f;
        private float _fadeStartTime;
        private float _fadeAmount = 0.0f;

        public enum AnimType
        {
            CorrectNormal,
            CorrectHard,
            CorrectSoft,
            TooHard,
            TooSoft,
        }

        public void Initialize(Color top, Color inner, Color particles, Color openParticles)
        {
            // Clear so Initialize is safe to re-call for live fret recoloring (e.g. the
            // settings preview's lefty flip); these lists are empty on the first call.
            _topMaterials.Clear();
            _innerMaterials.Clear();

            _originalUnityTopColor = top.ToUnityColor();
            _originalUnityInnerColor = inner.ToUnityColor();
            _originalEmissionColor = top.ToUnityColor() * 11.5f;

            // Set the top material color
            foreach (var material in ThemeBind.GetColoredMaterials())
            {
                material.color = _originalUnityTopColor;
                material.SetColor(_emissionColor, _originalEmissionColor);
                _topMaterials.Add(material);
            }

            // Set the inner material color
            foreach (var material in ThemeBind.GetInnerColoredMaterials())
            {
                material.color = _originalUnityInnerColor;
                _innerMaterials.Add(material);
            }

            // Set the particle colors
            ThemeBind.HitEffect.SetColor(particles.ToUnityColor());
            ThemeBind.OpenHitEffect.SetColor(openParticles.ToUnityColor());
            ThemeBind.SustainEffect.SetColor(particles.ToUnityColor());
            ThemeBind.PressedEffect.SetColor(particles.ToUnityColor());

            // See if certain parameters exist
            _hasPressedParam = ThemeBind.Animator.HasParameter(_pressed);
            _hasSustainParam = ThemeBind.Animator.HasParameter(_sustain);
            _hasOpenMissTrigger = ThemeBind.Animator.HasParameter(_openMiss);
        }

        // Initialize dual-half fret (primary + secondary)
        public void Initialize(Color top, Color inner, Color particles, Color openParticles,
            Color secondaryTop, Color secondaryInner, Color secondaryParticles)
        {
            // Primary half (existing logic)
            Initialize(top, inner, particles, openParticles);

            // Secondary half
            _secondaryTopMaterials.Clear();
            _secondaryInnerMaterials.Clear();
            _secondaryOriginalUnityTopColor = secondaryTop.ToUnityColor();
            _secondaryOriginalUnityInnerColor = secondaryInner.ToUnityColor();
            _secondaryOriginalEmissionColor = secondaryTop.ToUnityColor() * 11.5f;

            // Set secondary top materials
            foreach (var material in ThemeBind.GetSecondaryColoredMaterials())
            {
                material.color = _secondaryOriginalUnityTopColor;
                material.SetColor(_emissionColor, _secondaryOriginalEmissionColor);
                _secondaryTopMaterials.Add(material);
            }

            // Set secondary inner materials
            foreach (var material in ThemeBind.GetSecondaryInnerColoredMaterials())
            {
                material.SetColor(_secondaryColor, _secondaryOriginalUnityInnerColor);
                _secondaryInnerMaterials.Add(material);
            }

            // Set secondary particle colors
            if (ThemeBind.SecondaryPressedEffect != null)
            {
                ThemeBind.SecondaryPressedEffect.SetColor(secondaryParticles.ToUnityColor());
            }

            // Check for secondary pressed param
            _hasSecondaryPressedParam = ThemeBind.Animator.HasParameter(_secondaryPressed);
        }

        public void Update()
        {
            UpdateColor();
        }

        public void SetPressed(bool pressed)
        {
            SetPressed(pressed, pressed ? 1f : 0f);
        }

        public void SetPressedDrum(bool pressed, AnimType animType)
        {
            // Set the inner portion of the fret
            SetPressed(pressed, pressed ? GetFretInnerBrightnessMultiplier(animType) : 0f);
        }

        public void WhitenFretColor()
        {
            foreach (var material in _topMaterials)
            {
                material.color = UnityEngine.Color.white;
                material.SetColor(_emissionColor, UnityEngine.Color.white);
            }

            foreach (var material in _innerMaterials)
            {
                material.color = UnityEngine.Color.white;
                material.SetColor(_emissionColor, UnityEngine.Color.white);
            }

            foreach (var material in _secondaryTopMaterials)
            {
                material.color = UnityEngine.Color.white;
                material.SetColor(_secondaryEmissionColor, UnityEngine.Color.white);
            }

            foreach (var material in _secondaryInnerMaterials)
            {
                material.color = UnityEngine.Color.white;
                material.SetColor(_secondaryEmissionColor, UnityEngine.Color.white);
            }

            foreach (var light in ThemeBind.HitEffect.EffectLights)
            {
                light.IsBrightened = true;
            }

            foreach (var particle in ThemeBind.HitEffect.EffectParticles)
            {
                particle.BrightenColor();
            }
        }

        public void RestoreFretColor()
        {
            foreach (var material in _topMaterials)
            {
                material.color = _originalUnityTopColor;
                material.SetColor(_emissionColor, _originalEmissionColor);
            }

            foreach (var material in _innerMaterials)
            {
                material.color = _originalUnityInnerColor;
                material.SetColor(_emissionColor, _originalEmissionColor);
            }

            foreach (var material in _secondaryTopMaterials)
            {
                material.color = _secondaryOriginalUnityTopColor;
                material.SetColor(_secondaryEmissionColor, _secondaryOriginalEmissionColor);
            }

            foreach (var material in _secondaryInnerMaterials)
            {
                material.color = _secondaryOriginalUnityInnerColor;
                material.SetColor(_secondaryEmissionColor, _secondaryOriginalEmissionColor);
            }

            foreach (var light in ThemeBind.HitEffect.EffectLights)
            {
                light.IsBrightened = false;
            }

            foreach (var particle in ThemeBind.HitEffect.EffectParticles)
            {
                particle.RestoreColor();
            }
        }

        private float GetFretInnerBrightnessMultiplier(AnimType animType)
        {
            // Normal and hard should both be 1f
            return AnimType.CorrectSoft == animType ? 0f : 1f;
        }

        public void SetPressed(bool pressed, float value)
        {
            foreach (var material in _innerMaterials)
            {
                material.SetFloat(_fade, value);
            }

            if (_hasPressedParam)
            {
                ThemeBind.Animator.SetBool(_pressed, pressed);
            }

            if (pressed)
            {
                ThemeBind.PressedEffect.Play();
            }
            else
            {
                ThemeBind.PressedEffect.Stop();
            }
        }

        // Dimmer than a correct hit's full brightness (see GetFretInnerBrightnessMultiplier,
        // which returns 1f) so a genuine overhit (wrong pad, or wrong-timing kick hit) still
        // reads as pressed/reacting without looking as confirmed as an actual correct hit.
        private const float OVERHIT_INNER_LIGHT_BRIGHTNESS = 0.15f;

        /// <summary>
        /// Lights up (or turns off) this fret's inner part the same way a correct hit does (see
        /// SetPressedDrum/SetPressed), but dimmed - used for a genuine overhit (wrong pad, or
        /// wrong-timing kick hit - see DrumsPlayer.OnPadHit/UpdateFretArray). Driven by
        /// DrumsPlayer's own continuous per-frame time-tracking (mirroring how a correct hit's
        /// light-up already works), rather than a one-shot timer here - a fret's inner light is
        /// re-set unconditionally every frame by that system, so a standalone timed coroutine
        /// here would just get immediately overwritten back off.
        /// </summary>
        public void SetOverhitPressed(bool pressed)
        {
            SetPressed(pressed, pressed ? OVERHIT_INNER_LIGHT_BRIGHTNESS : 0f);
        }

        // Secondary half press (white fret half)
        public void SetPressedSecondary(bool pressed, float value)
        {
            foreach (var material in _secondaryInnerMaterials)
            {
                material.SetFloat(_secondaryFade, value);
            }

            if (_hasSecondaryPressedParam)
            {
                ThemeBind.Animator.SetBool(_secondaryPressed, pressed);
            }

            if (pressed && ThemeBind.SecondaryPressedEffect != null)
            {
                ThemeBind.SecondaryPressedEffect.Play();
            }
            else if (ThemeBind.SecondaryPressedEffect != null)
            {
                ThemeBind.SecondaryPressedEffect.Stop();
            }
        }

        public void PlayHitAnimation()
        {
            // Cuts off any still-playing MissEffect burst from a previous overhit on this same
            // pad immediately (see PlayMissParticles/PlayOverhitAnimation) - otherwise, on dense
            // charts, its ~1s decay can still be visibly alive by the time a genuine hit lands
            // on the same pad shortly after, making the miss indicator incorrectly read as if it
            // were part of the hit.
            ThemeBind.MissEffect.StopImmediate();
            ThemeBind.Animator.SetTrigger(_hit);
        }

        public void PlayCymbalHitAnimation()
        {
            ThemeBind.MissEffect.StopImmediate();
            ThemeBind.Animator.SetTrigger(_cymbalHit);
        }

        // Extra scale "pop" used only by Circular drums (see PlayDrumJumpBoost below) - layered
        // on top of the Hit/CymbalHit Animator jump instead of replacing it, since that clip is
        // shared verbatim with guitar (Circular has no drums-only override controller, unlike
        // Rectangular) and can't be safely amplified in the asset itself without affecting
        // guitar's own strum-fret jump too.
        private const float DRUM_JUMP_BOOST_DURATION = 0.12f;
        // Also works around CymbalHit not existing as an Animator parameter on Circular's
        // shared controller at all (only Hit/Miss/Pressed/Sustain do) - meaning cymbal hits
        // currently trigger zero jump there on their own; this plays unconditionally off the
        // Hit/CymbalHit trigger, so both toms and cymbals always get a consistent, noticeable
        // pop regardless.
        private const float DRUM_JUMP_BOOST_SCALE = 1.35f;

        private Vector3?  _drumJumpBaseLocalScale;
        private Coroutine _drumJumpBoostCoroutine;

        /// <summary>
        /// Briefly scales this fret up then eases back down to its own already-configured
        /// baseline scale (each fret's own per-lane width - see FretArray's layout code - is
        /// baked into transform.localScale.x, so this multiplies relative to that rather than
        /// resetting to a flat (1,1,1)). Safe to call again before a previous pop finishes - it
        /// always eases back to the same captured baseline instead of compounding.
        /// </summary>
        public void PlayDrumJumpBoost()
        {
            // Captured lazily (not in Awake) since FretArray only sets this fret's own per-lane
            // width scale right after creating it - by the time any hit can occur, that's
            // already been applied, so this always captures the correct rest-state baseline.
            _drumJumpBaseLocalScale ??= transform.localScale;

            if (_drumJumpBoostCoroutine != null)
            {
                StopCoroutine(_drumJumpBoostCoroutine);
            }

            _drumJumpBoostCoroutine = StartCoroutine(DrumJumpBoostRoutine());
        }

        private IEnumerator DrumJumpBoostRoutine()
        {
            var baseScale = _drumJumpBaseLocalScale!.Value;
            var elapsed = 0f;

            while (elapsed < DRUM_JUMP_BOOST_DURATION)
            {
                var t = elapsed / DRUM_JUMP_BOOST_DURATION;
                // Symmetric rise-then-fall (peaks at the midpoint) - a quick, punchy pop rather
                // than an abrupt snap back to baseline.
                var boost = Mathf.Sin(t * Mathf.PI) * (DRUM_JUMP_BOOST_SCALE - 1f);
                transform.localScale = baseScale * (1f + boost);

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = baseScale;
            _drumJumpBoostCoroutine = null;
        }

        public void PlayHitParticles()
        {
            ThemeBind.HitEffect.Play();
        }

        /// <summary>
        /// Plays the hit particles with the given color. Used by six-fret so the
        /// burst matches the note hit: black for black frets, white for white
        /// frets, and a black/white mix for barres.
        /// </summary>
        public void PlayHitParticles(UnityEngine.Color color)
        {
            ThemeBind.HitEffect.SetColor(color);
            ThemeBind.HitEffect.Play();
        }

        public void PlayFullWidthHitParticles()
        {
            ThemeBind.OpenHitEffect.Play();
        }

        public void PlayMissAnimation()
        {
            ThemeBind.Animator.SetTrigger(_miss);
        }

        public void PlayMissParticles()
        {
            ThemeBind.MissEffect.Play();
        }

        public void PlayOpenMissAnimation()
        {
            if (_hasOpenMissTrigger)
            {
                ThemeBind.Animator.SetTrigger(_openMiss);
            }
            else
            {
                PlayMissAnimation();
            }
        }

        public void PlayOpenMissParticles()
        {
            ThemeBind.OpenMissEffect.Play();
        }

        public void SetSustained(bool sustained)
        {
            if (sustained)
            {
                ThemeBind.SustainEffect.Play();
            }
            else
            {
                ThemeBind.SustainEffect.Stop();
            }

            if (_hasSustainParam)
            {
                ThemeBind.Animator.SetBool(_sustain, sustained);
            }
        }

        private readonly Dictionary<EffectGroup, Coroutine> _timedEffectBurstCoroutines = new();

        /// <summary>
        /// Briefly plays an effect group's particles/lights then stops them, without the
        /// group ever entering a real sustain state (no Animator "Sustain" bool change - see
        /// SetSustained above). Used by Circular's kick animation to flash every fret's own
        /// Sustain Effect in unison (dimmed/flattened - see ApplyCircularDrumHitEffectTuning
        /// below), on top of their normal hit-jump animation.
        /// </summary>
        public void PlayTimedEffectBurst(EffectGroup effect, float duration)
        {
            if (_timedEffectBurstCoroutines.TryGetValue(effect, out var existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            _timedEffectBurstCoroutines[effect] = StartCoroutine(TimedEffectBurstRoutine(effect, duration));
        }

        private IEnumerator TimedEffectBurstRoutine(EffectGroup effect, float duration)
        {
            effect.ZeroStartDelay();
            effect.Play();
            // Skip past the particle system's own emission ramp-up before the player ever sees
            // a frame of it - these particles emit continuously at a steady rate rather than in
            // an instant burst, so right after Play() there are initially zero/few particles on
            // screen until enough real time passes for the first ones to spawn, which reads as
            // a slow start. Fast-forwarding the simulation clock by about one particle lifetime
            // puts it in the same "steady state" of particle density it would reach on its own
            // after a moment, so the first rendered frame already looks fully present/instant.
            effect.FastForward(EFFECT_BURST_SKIP_SECONDS);
            yield return new WaitForSeconds(duration);
            // A hard stop-and-clear (not the gentler Stop(), which lets already-alive particles
            // decay naturally) so the burst ends right at its intended duration instead of
            // visibly lingering/fading out afterward.
            effect.StopImmediate();
            _timedEffectBurstCoroutines[effect] = null;
        }

        // Circular reuses the guitar fret's Flame/Ember hit particles for drum hits (it has no
        // themed drum particle set of its own), so they get a small, Circular-specific hold-time
        // and brightness boost here, matched only by exact name - any theme without particles
        // named "Flame Particles"/"Ember Particles" (e.g. Rectangular's "Ring"/"Smoke"/"Flash")
        // is completely unaffected by this.
        // Set to roughly the sustain-burst particles' own ~0.15s start lifetime (see
        // ApplyCircularDrumHitEffectTuning below) - long enough to reach their steady-state
        // density, short enough to still be a single simulate call.
        private const float EFFECT_BURST_SKIP_SECONDS = 0.15f;

        private static readonly Dictionary<string, (float Lifetime, float Brightness)> _circularDrumHitParticleFactors = new()
        {
            ["Flame Particles"] = (1.3f, 1.4f),
            ["Ember Particles"] = (1.3f, 1.4f),
        };

        // The sustain burst reuses the guitar sustain-hold flame, which is authored much
        // taller/brighter than a quick drum flash should be - dim and flatten it considerably so
        // it reads as a short, subtle burst rather than a held column of fire. Applied to every
        // particle in the group (not matched by name like the hit effect above) since, for
        // drums, this EffectGroup is only ever used by Circular's kick burst - drums have no
        // real per-note sustain state to otherwise worry about affecting.
        private const float SUSTAIN_BURST_BRIGHTNESS_FACTOR = 0.6f;
        // Bumped from 0.2 - read as too short/flat vertically at that value.
        private const float SUSTAIN_BURST_HEIGHT_FACTOR = 0.35f;
        // Keeps the burst's footprint contained to roughly its own button rather than the much
        // wider column the guitar sustain flame is originally authored as. These particles are
        // literally named "Outer/Inner Spark Particles" - individual radiating spike/streak
        // shapes, not a solid blob - so matching them to the button's *exact* full width (ratio
        // 1) made each spike long enough to visibly stretch into neighboring buttons once every
        // button fires this burst together on a kick (confirmed via screenshot: thin red lines
        // crossing the whole row).
        private const float SUSTAIN_BURST_WIDTH_RATIO = 0.5f;


        /// <summary>
        /// Instance method (not static, unlike before) since Circular's kick burst tuning now
        /// needs this Fret's own measured button width/position as a sizing/placement reference
        /// - see SetStartSizeXZ/PositionAtWorldHeight on EffectParticle for why world-space
        /// measurements from this Fret are used instead of further guessing at the sustain
        /// effect's own confusing, deeply-nested authored local space.
        /// </summary>
        public void ApplyCircularDrumHitEffectTuning()
        {
            foreach (var particle in ThemeBind.HitEffect.EffectParticles)
            {
                if (!_circularDrumHitParticleFactors.TryGetValue(particle.name, out var factors)) continue;

                particle.ScaleStartLifetime(factors.Lifetime);
                particle.ScaleStartColorBrightness(factors.Brightness);
            }

            var targetWidth = GetOwnButtonWorldWidth() * SUSTAIN_BURST_WIDTH_RATIO;
            var targetHeightY = transform.position.y;

            foreach (var particle in ThemeBind.SustainEffect.EffectParticles)
            {
                particle.ScaleStartColorBrightness(SUSTAIN_BURST_BRIGHTNESS_FACTOR);
                particle.ScaleStartSizeY(SUSTAIN_BURST_HEIGHT_FACTOR);
                particle.SetStartSizeXZ(targetWidth);
                particle.PositionAtWorldHeight(targetHeightY);
            }
        }

        // This Fret's own rendered mesh width, in world units - see ApplyCircularDrumHitEffectTuning
        // above. MeshRenderer only (not the generic Renderer), for the same reason as
        // FretArray.GetFretButtonWorldBounds: this Fret's children also include idle
        // ParticleSystemRenderers, whose bounds are degenerate/empty while nothing is emitting.
        private float GetOwnButtonWorldWidth()
        {
            var hasBounds = false;
            var bounds = new Bounds();

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds.size.x : 0f;
        }


        public void DimColor(bool fade = true)
        {
            _active = false;
            _fadeDirection = false;
            _colorChangeEnabled = true;
            _fadeAmount = 0.0f;

            if (fade)
            {
                FadeColor(_fadeDuration, false, false);
            }
            else
            {
                foreach (var material in _topMaterials)
                {
                    material.color = _inactiveColor;
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = _inactiveColor;
                }
            }
        }

        public void ResetColor(bool fade = false)
        {
            _active = true;
            _fadeDirection = true;
            _colorChangeEnabled = false;
            _fadeAmount = 0.0f;

            if (fade)
            {
                FadeColor(_fadeDuration, false, true);
            }
            else
            {
                foreach (var material in _topMaterials)
                {
                    material.color = _originalUnityTopColor;
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = _originalUnityInnerColor;
                }
            }
        }

        public void SetBreMode(bool breMode)
        {
            ThemeBind.SetBreMode(breMode);
        }

        /// <summary>
        /// Fades or pulses the fret color between the normal color and inactive color
        ///
        /// Note that the pulse happens only once per call, it does not continue indefinitely.
        /// </summary>
        /// <param name="duration">Length of time transition will take</param>
        /// <param name="pulse">Whether to pulse or to fade once</param>
        /// <param name="fadeDirection">True fades in, false fades out (default true)</param>
        public void FadeColor(float duration, bool pulse, bool fadeDirection = true)
        {
            if (_active && pulse)
            {
                // Can't pulse if the fret is already active
                return;
            }

            _pulseOrFade = pulse;
            _fadeDuration = duration;
            _fadeDirection = fadeDirection;
            _fadeStartTime = Time.time;
            _colorChangeEnabled = true;
            _fadeAmount = 0.0f;
        }

        // TODO: Investigate whether we should be using a MaterialPropertyBlock to set the color
        //  instead of setting the color directly so that draw call batching is possible
        //  (I think it doesn't actually matter much since there's only 10 of these materials active at a time,
        //   but every bit helps, I guess?)
        public void UpdateColor()
        {
            if (!_colorChangeEnabled)
            {
                return;
            }

            var rateAdjustment = 1; //_pulseOrFade ? 2 : 1;

            _fadeAmount += Time.deltaTime / (_fadeDuration / rateAdjustment);
            var fadeIntensity = _pulseOrFade ? _fadeAmount : ((Mathf.Cos(Mathf.PI * _fadeAmount) / 2) * -1) + 1;

            if (_fadeDirection)
            {
                // Fading in
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, fadeIntensity);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_inactiveColor, _originalUnityTopColor, fadeIntensity);
                }
            }
            else
            {
                // Fading out
                foreach (var material in _topMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, fadeIntensity);
                }

                foreach (var material in _innerMaterials)
                {
                    material.color = UnityEngine.Color.Lerp(_originalUnityTopColor, _inactiveColor, fadeIntensity);
                }
            }

            if (Time.time - _fadeStartTime >= _fadeDuration)
            {
                _colorChangeEnabled = false;
                _fadeAmount = 0.0f;

                if (_fadeDirection)
                {
                    // Fading in
                    foreach (var material in _topMaterials)
                    {
                        material.color = _originalUnityTopColor;
                    }

                    foreach (var material in _innerMaterials)
                    {
                        // Why was this _originalUnityTopColor?
                        material.color = _originalUnityInnerColor;
                    }
                }
                else
                {
                    // Fading out
                    foreach (var material in _topMaterials)
                    {
                        material.color = _inactiveColor;
                    }

                    foreach (var material in _innerMaterials)
                    {
                        material.color = _inactiveColor;
                    }
                }
            }
        }
    }
}
