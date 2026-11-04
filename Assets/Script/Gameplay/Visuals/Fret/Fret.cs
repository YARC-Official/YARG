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

        // The "Hit Effects" particle group is shared with guitar/keys via the same theme prefab,
        // so drum-specific tuning is applied here at runtime (see ApplyDrumHitEffectTuning)
        // instead of changing the shared asset's values. Only particles with these exact names
        // are affected, so other themes' differently-named particles are left untouched.
        //
        // Lifetime is kept well under guitar's full (1x) duration for a snappier feel, except
        // Ring, which stays at the shorter original 0.48 (a longer hold reads as an expanding
        // halo that grows too wide). Brightness pushes each particle's RGB into HDR range so
        // the hit reads as more vivid/punchy. Sparkles additionally needs Additive (its plain
        // alpha-blended material otherwise clamps at opaque white regardless of HDR push),
        // SizeY to stretch its thin streaks taller without widening them (widening makes them
        // read as blobby dots instead of lines), and AlphaBoost to counter its authored Color
        // Over Lifetime curve, which fades to near-zero alpha for most of the streak's life.
        private static readonly Dictionary<string, (float Lifetime, float? Velocity, float? Brightness, bool Additive, float? SizeY, float? AlphaBoost)> _drumHitParticleFactors = new()
        {
            ["Ring"]        = (0.48f, null, 1.3f, false, null, null),
            ["Smoke"]       = (0.7f, null, null, false, null, null),
            ["Flash"]       = (0.7f, null, 2.3f, false, null, null),
            ["White Smoke"] = (0.7f, null, 1.9f, false, null, null),
            ["Sparkles"]    = (0.72f, 1.5f, 6.2f, true, 1.6f, 0.7f),
            ["Shards"]      = (0.72f, 1.5f, null, false, null, null),
        };


        // The Hit/Miss Effects groups are parented to the outer fret prefab root, which is a
        // sibling (not an ancestor) of the Animator's GameObject - so neither FretHitDrums.anim's
        // jump curve nor FretMiss.anim's tumble (both of which target this path, relative to the
        // Animator) can ever move Hit/Miss Effects directly. Instead, each frame we sample how
        // far this mesh has actually moved from its resting world position, and apply that same
        // world-space delta to Hit/Miss Effects, so each stays visually anchored to the fret cap
        // regardless of any rotation/scale in between. Without this, Miss Effects' light stayed
        // fixed in place while FretMiss.anim spun the fret cap away from under it, reading as a
        // small ball of light floating apart from the (now-rotated-away) button.
        private const string DRUM_JUMP_MESH_PATH = "RectangularFret/Drum_4_Fret_1_Color.001";

        // Safety cutoff for hit jump tracking, comfortably longer than FretHitDrums.anim's
        // current stop time so the full return-to-rest is always captured even if that clip is
        // retuned.
        private const float DRUM_HIT_JUMP_TRACKING_DURATION = 0.35f;

        // Amplifies the tracked world-space delta so the flare's movement reads as more
        // pronounced than the mesh's actual (fairly subtle) jump.
        private const float DRUM_HIT_JUMP_TRACKING_STRENGTH = 1.8f;

        // Safety cutoff for miss jump tracking, comfortably longer than FretMissDrums.anim's
        // current stop time (0.15s) so the full return-to-rest is always captured even if that
        // clip is retuned.
        private const float DRUM_MISS_JUMP_TRACKING_DURATION = 0.35f;

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

        // See DRUM_JUMP_MESH_PATH above. Null/inactive when this isn't a drum fret, or when the
        // mesh path couldn't be found (e.g. a different theme), so tracking is simply skipped.
        private Transform _drumJumpMeshTransform;
        private Vector3   _drumJumpMeshRestPosition;
        private Vector3   _hitEffectRestPosition;
        private bool      _hitJumpTrackingActive;
        private float     _hitJumpTrackingTimer;
        private Vector3   _missEffectRestPosition;
        private bool      _missJumpTrackingActive;
        private float     _missJumpTrackingTimer;

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

            if (_hitJumpTrackingActive)
            {
                UpdateHitEffectJumpTracking();
            }

            if (_missJumpTrackingActive)
            {
                UpdateMissEffectJumpTracking();
            }
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
            ThemeBind.Animator.SetTrigger(_hit);
            StartHitEffectJumpTracking();
        }

        public void PlayCymbalHitAnimation()
        {
            ThemeBind.Animator.SetTrigger(_cymbalHit);
            StartHitEffectJumpTracking();
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

        /// <summary>
        /// Scales this fret's hit-effect particles down to the shorter, drum-tuned
        /// duration/range. Should only be called for drum frets (toms/cymbals). Also looks up
        /// the mesh that FretHitDrums.anim/FretMiss.anim animate, so Hit and Miss Effects can
        /// each follow it (see DRUM_JUMP_MESH_PATH above); if that mesh can't be found (e.g. a
        /// different theme), jump tracking is simply skipped for both.
        /// </summary>
        public void ApplyDrumHitEffectTuning()
        {
            ApplyDrumHitEffectTuning(ThemeBind.HitEffect);

            _drumJumpMeshTransform = ThemeBind.Animator.transform.Find(DRUM_JUMP_MESH_PATH);
            if (_drumJumpMeshTransform != null)
            {
                _drumJumpMeshRestPosition = _drumJumpMeshTransform.position;
                _hitEffectRestPosition = ThemeBind.HitEffect.transform.position;
                _missEffectRestPosition = ThemeBind.MissEffect.transform.position;
            }
        }

        /// <summary>
        /// Scales the given hit-effect particle group down to the shorter, drum-tuned
        /// duration/range. Also used by <see cref="KickFret"/>, which instantiates its own
        /// copy of the hit-effect group at runtime. Only particles matching one of the names
        /// in <see cref="_drumHitParticleFactors"/> are affected, so this is a no-op for any
        /// unrelated particles (e.g. from another theme).
        /// </summary>
        public static void ApplyDrumHitEffectTuning(EffectGroup hitEffect)
        {
            foreach (var particle in hitEffect.EffectParticles)
            {
                if (!_drumHitParticleFactors.TryGetValue(particle.name, out var factors)) continue;

                particle.ScaleStartLifetime(factors.Lifetime);
                if (factors.Velocity is { } velocityFactor)
                {
                    particle.ScaleVelocityOverLifetime(velocityFactor);
                }
                if (factors.Additive)
                {
                    particle.MakeAdditive();
                }
                if (factors.Brightness is { } brightnessFactor)
                {
                    particle.ScaleStartColorBrightness(brightnessFactor);
                    // Also rebase InitialColor by the same factor so the boost survives the
                    // fret's accent-whiten flash (see Fret.WhitenFretColor/RestoreFretColor) -
                    // otherwise RestoreColor() resets this particle's startColor straight back
                    // to the plain, un-boosted color the next time that flash plays, silently
                    // discarding the brightness boost for the rest of the session.
                    particle.RebaseInitialColorBrightness(brightnessFactor);
                }
                if (factors.SizeY is { } sizeYFactor)
                {
                    particle.ScaleStartSizeY(sizeYFactor);
                }
                if (factors.AlphaBoost is { } alphaBoost)
                {
                    particle.BoostColorOverLifetimeAlpha(alphaBoost);
                }
            }
        }

        private void StartHitEffectJumpTracking()
        {
            if (_drumJumpMeshTransform == null)
            {
                return;
            }

            _hitJumpTrackingTimer = 0f;
            _hitJumpTrackingActive = true;
        }

        private void UpdateHitEffectJumpTracking()
        {
            _hitJumpTrackingTimer += Time.deltaTime;

            if (_hitJumpTrackingTimer >= DRUM_HIT_JUMP_TRACKING_DURATION)
            {
                _hitJumpTrackingActive = false;
                ThemeBind.HitEffect.transform.position = _hitEffectRestPosition;
                return;
            }

            // Replicate however far (and in whatever direction) the actual jump has moved the
            // mesh, in world space - this stays correct regardless of the rotation/scale sitting
            // between the mesh and Hit Effects, since Transform.position already accounts for it.
            var worldDelta = _drumJumpMeshTransform.position - _drumJumpMeshRestPosition;
            ThemeBind.HitEffect.transform.position = _hitEffectRestPosition + worldDelta * DRUM_HIT_JUMP_TRACKING_STRENGTH;
        }

        public void PlayMissAnimation()
        {
            ThemeBind.Animator.SetTrigger(_miss);
            StartMissEffectJumpTracking();
        }

        private void StartMissEffectJumpTracking()
        {
            if (_drumJumpMeshTransform == null)
            {
                return;
            }

            _missJumpTrackingTimer = 0f;
            _missJumpTrackingActive = true;
        }

        private void UpdateMissEffectJumpTracking()
        {
            _missJumpTrackingTimer += Time.deltaTime;

            if (_missJumpTrackingTimer >= DRUM_MISS_JUMP_TRACKING_DURATION)
            {
                _missJumpTrackingActive = false;
                ThemeBind.MissEffect.transform.position = _missEffectRestPosition;
                return;
            }

            // Unlike Hit Effects, this isn't amplified - the goal here is just to keep the Miss
            // light visually attached to the fret cap as FretMiss.anim tumbles it around, not to
            // exaggerate the motion.
            var worldDelta = _drumJumpMeshTransform.position - _drumJumpMeshRestPosition;
            ThemeBind.MissEffect.transform.position = _missEffectRestPosition + worldDelta;
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
