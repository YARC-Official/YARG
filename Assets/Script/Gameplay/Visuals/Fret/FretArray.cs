using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Gameplay.Visuals
{
    public readonly struct HighwayOrderingInfo
    {
        public HighwayOrderingInfo(int position, int colorIndex)
        {
            Position = position;
            ColorIndex = colorIndex;
        }

        public int Position { get; }
        public int ColorIndex { get; }
    }

    public class FretArray : MonoBehaviour
    {
        private const float WIDTH_NUMERATOR   = 2f;
        private const float WIDTH_DENOMINATOR = 5f;

        public bool DontFlipColorsLeftyFlip;
        public bool UseKickFrets;

        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private Transform _leftKickFretPosition;
        [SerializeField]
        private Transform _rightKickFretPosition;

        private readonly Dictionary<int, Fret> _frets = new();
        private readonly List<KickFret> _kickFrets = new();
        private readonly List<(Fret Fret, int PrimaryColorIndex, int SecondaryColorIndex)> _colorBindings = new();

        private readonly List<int> _activeFrets  = new();
        private readonly List<int> _pulsingFrets = new();
        private          float     _pulseDuration;

        private readonly HashSet<int> _usedFretIndexes = new();

        /*
         * Overload for instruments where lefty flip does not affect color (e.g. a lefty-flipped Green Fret on 5F Guitar is still green, just laterally shifted).
         * Acts as a convenience so that you can just pass in a mapping of note type to lateral position, and it will create HighwayOrderingInfos that use the note
         * type as the color index.
         *
         * On Drums, lefty flip affects position and color separately (e.g. a lefty flipped Red Drum becomes green in addition to being shifted), so you need to
         * provide HighwayOrderingInfos directly.
         */
        #nullable enable
        public void Initialize(Dictionary<int, int> highwayOrdering, int laneCount, GameObject? kickFretPrefab,
            IFretColorProvider fretColorProvider, ThemePreset themePreset, VisualStyle style, bool dualHalfFrets = false)
        {
            var derivedDictionary = new Dictionary<int, HighwayOrderingInfo>();

            foreach (var (noteType, position) in highwayOrdering)
            {
                derivedDictionary.Add(noteType, new(position, noteType));
            }

            Initialize(derivedDictionary, laneCount, kickFretPrefab, fretColorProvider, themePreset, style, dualHalfFrets);
        }

        public void Initialize(Dictionary<int, HighwayOrderingInfo> highwayOrdering, int laneCount,
            GameObject? kickFretPrefab, IFretColorProvider fretColorProvider, ThemePreset themePreset, VisualStyle style)
        {
            Initialize(highwayOrdering, laneCount, kickFretPrefab, fretColorProvider, themePreset, style, false);
        }

        // Overload with dual-half fret support (for 6-fret guitar)
        public void Initialize(Dictionary<int, HighwayOrderingInfo> highwayOrdering, int laneCount,
            GameObject? kickFretPrefab, IFretColorProvider fretColorProvider, ThemePreset themePreset, VisualStyle style,
            bool dualHalfFrets)
        {
            var fretPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, style);

            _frets.Clear();
            _colorBindings.Clear();
            foreach (var (noteType, highwayOrderingInfo) in highwayOrdering)
            {
                // Check if position was already added (barre chords share the same physical fret object across lanes)
                if (!_usedFretIndexes.Add(highwayOrderingInfo.Position))
                {
                    // Find an earlier note type at this same position and reuse its fret component
                    foreach (var (otherNoteType, otherHighwayOrderingInfo) in highwayOrdering)
                    {
                        if (otherHighwayOrderingInfo.Position == highwayOrderingInfo.Position)
                        {
                            _frets[noteType] = _frets[otherNoteType];
                            break;
                        }
                    }

                    continue;
                }
                var fret = Instantiate(fretPrefab, transform);
                fret.SetActive(true);

                // Position
                float x = _trackWidth / laneCount * highwayOrderingInfo.Position - _trackWidth / 2f + 1f / laneCount;
                fret.transform.localPosition = new Vector3(x, 0f, 0f);

                // Scale
                float scale = (_trackWidth / WIDTH_NUMERATOR) / (laneCount / WIDTH_DENOMINATOR);
                fret.transform.localScale = new Vector3(scale, 1f, 1f);

                var fretComp = fret.GetComponent<Fret>();

                if (dualHalfFrets)
                {
                    // Primary = first fret (Black), Secondary = second fret (White)
                    // Since enum order is Black1, White1, Black2, etc., first occurrence is Black
                    var primaryColor = fretColorProvider.GetFretColor(highwayOrderingInfo.ColorIndex);
                    var primaryInner = fretColorProvider.GetFretInnerColor(highwayOrderingInfo.ColorIndex);
                    var primaryParticles = fretColorProvider.GetParticleColor(highwayOrderingInfo.ColorIndex);

                    // Find secondary color (next fret in same position)
                    int secondaryColorIndex = -1;
                    foreach (var (otherNoteType, otherInfo) in highwayOrdering)
                    {
                        if (otherInfo.Position == highwayOrderingInfo.Position && otherNoteType != noteType)
                        {
                            secondaryColorIndex = otherInfo.ColorIndex;
                            break;
                        }
                    }

                    var secondaryColor = secondaryColorIndex != -1
                        ? fretColorProvider.GetFretColor(secondaryColorIndex)
                        : primaryColor;
                    var secondaryInner = secondaryColorIndex != -1
                        ? fretColorProvider.GetFretInnerColor(secondaryColorIndex)
                        : primaryInner;
                    var secondaryParticles = secondaryColorIndex != -1
                        ? fretColorProvider.GetParticleColor(secondaryColorIndex)
                        : primaryParticles;

                    _colorBindings.Add((fretComp, highwayOrderingInfo.ColorIndex, secondaryColorIndex));
                    fretComp.Initialize(
                        primaryColor, primaryInner, primaryParticles,
                        fretColorProvider.GetParticleColor((int)FiveFretGuitarFret.Open),
                        secondaryColor, secondaryInner, secondaryParticles
                    );
                }
                else
                {
                    _colorBindings.Add((fretComp, highwayOrderingInfo.ColorIndex, -1));
                    fretComp.Initialize(
                        fretColorProvider.GetFretColor(highwayOrderingInfo.ColorIndex),
                        fretColorProvider.GetFretInnerColor(highwayOrderingInfo.ColorIndex),
                        fretColorProvider.GetParticleColor(highwayOrderingInfo.ColorIndex),
                        fretColorProvider.GetParticleColor((int)FiveFretGuitarFret.Open)
                    );
                }

                _frets[noteType] = fretComp;

                // Circular reuses the guitar fret's hit particles for drum hits (no themed
                // drum particle set of its own) - give tom/cymbal hits a small boost there so
                // they read more clearly. Matched only by particle name (see
                // Fret.ApplyCircularDrumHitEffectTuning), so this is a no-op for every other
                // theme/style.
                if (style is VisualStyle.FourLaneDrums or VisualStyle.FiveLaneDrums
                    && themePreset.AssetBundleThemePath == "Themes/Circular")
                {
                    fretComp.ApplyCircularDrumHitEffectTuning();
                }
            }

            _kickFrets.Clear();
            if (kickFretPrefab is not null && UseKickFrets)
            {
                // Spawn in kick frets
                var leftKick = Instantiate(kickFretPrefab, transform);
                leftKick.SetActive(true);
                var rightKick = Instantiate(kickFretPrefab, transform);
                rightKick.SetActive(true);

                // Position kick frets
                leftKick.transform.localPosition = _leftKickFretPosition.localPosition;
                rightKick.transform.localPosition = _rightKickFretPosition.localPosition;
                rightKick.transform.localScale = rightKick.transform.localScale.InvertX();

                // Add kick frets
                _kickFrets.Add(leftKick.GetComponent<KickFret>());
                _kickFrets.Add(rightKick.GetComponent<KickFret>());
            }

            // Start with all frets active, they will be set inactive once TrackPlayer figures itself out
            foreach (var fretIdx in _frets.Keys)
            {
                _activeFrets.Add(fretIdx);
            }

            foreach (var kickFret in _kickFrets)
            {
                // 0 resolves to kick in both FourLaneDrumsFret and FiveFretDrumsFret, so it isn't worth doing this the "right"
                // way via an extra argument. If another format comes along that wants something other than 0 for the kick fret
                // color profile index, this should be updated to remove the magic number
                kickFret.Initialize(fretColorProvider.GetFretColor(0));
            }
        }
        #nullable restore

        /// <summary>
        /// Re-applies colors to the already-created frets, optionally remapping which
        /// color index each fret uses. Used by the settings preview to reverse the fret
        /// color order for lefty flip without rebuilding the fret array.
        /// </summary>
        public void RecolorFrets(IFretColorProvider fretColorProvider, Func<int, int> colorIndexRemap = null)
        {
            foreach (var (fret, primaryColorIndex, secondaryColorIndex) in _colorBindings)
            {
                int colorIndex = colorIndexRemap != null ? colorIndexRemap(primaryColorIndex) : primaryColorIndex;
                var top = fretColorProvider.GetFretColor(colorIndex);
                var inner = fretColorProvider.GetFretInnerColor(colorIndex);
                var particles = fretColorProvider.GetParticleColor(colorIndex);
                var openParticles = fretColorProvider.GetParticleColor((int)FiveFretGuitarFret.Open);

                if (secondaryColorIndex >= 0)
                {
                    int secondaryIndex = colorIndexRemap != null ? colorIndexRemap(secondaryColorIndex) : secondaryColorIndex;
                    fret.Initialize(top, inner, particles, openParticles,
                        fretColorProvider.GetFretColor(secondaryIndex),
                        fretColorProvider.GetFretInnerColor(secondaryIndex),
                        fretColorProvider.GetParticleColor(secondaryIndex));
                }
                else
                {
                    fret.Initialize(top, inner, particles, openParticles);
                }
            }

            foreach (var kickFret in _kickFrets)
            {
                kickFret.Initialize(fretColorProvider.GetFretColor(0));
            }
        }

        public void SetPressed(int index, bool pressed)
        {
            _frets[index].SetPressed(pressed);
        }

        public void SetPressedSecondary(int index, bool pressed)
        {
            _frets[index].SetPressedSecondary(pressed, pressed ? 1f : 0f);
        }

        public void SetPressedSecondary(int index, bool pressed, float value)
        {
            _frets[index].SetPressedSecondary(pressed, value);
        }

        // Used for frets that can only receive a momentary input rather than being held. This includes all
        // drum frets, as well as the dedicated open lane fret for 5F Guitar (no, holding the strum bar down
        // doesn't count)
        public void SetPressedImpulse(int index, bool pressed, Fret.AnimType animType)
        {
            _frets[index].SetPressedDrum(pressed, animType);
        }

        public void SetSustained(int index, bool sustained)
        {
            _frets[index].SetSustained(sustained);
        }

        public void PlayHitAnimation(int index)
        {
            _frets[index].PlayHitAnimation();
            _frets[index].PlayHitParticles();
        }

        /// <summary>
        /// Plays the hit animation and particles with an explicit particle color
        /// (six-fret colors the burst based on which note was hit).
        /// </summary>
        public void PlayHitAnimation(int index, Color particleColor)
        {
            _frets[index].PlayHitAnimation();
            _frets[index].PlayHitParticles(particleColor);
        }

        public void PlayCodaHitAnimation(int index)
        {
            if (!_frets.TryGetValue(index, out var fret))
            {
                return;
            }

            fret.PlayHitAnimation();
            fret.PlayHitParticles();
        }

        public void PlayCymbalHitAnimation(int index)
        {
            _frets[index].PlayCymbalHitAnimation();
            _frets[index].PlayHitParticles();
        }

        /// <summary>
        /// Extra scale "pop" layered on top of a tom/cymbal hit's own Animator-driven jump -
        /// see Fret.PlayDrumJumpBoost. Used only by Circular drums (see DrumsPlayer's
        /// AnimateAction/AnimateFret), since Circular's shared FretHitGuitar.anim clip (used by
        /// both guitar and drums) has too subtle a jump on its own to safely amplify without
        /// also affecting guitar.
        /// </summary>
        public void PlayDrumJumpBoost(int index)
        {
            _frets[index].PlayDrumJumpBoost();
        }


        public void PlayFullWidthHitAnimation()
        {
            foreach (var (_, fret) in _frets)
            {
                fret.PlayHitAnimation();
                fret.PlayFullWidthHitParticles();
            }
        }

        public void PlayMissAnimation(int index)
        {
            if (_frets.ContainsKey(index))
            {
                _frets[index].PlayMissAnimation();
                _frets[index].PlayMissParticles();
            }
        }

        /// <summary>
        /// Used by Circular drums for a genuine overhit (pressing a pad with no note there at
        /// all to hit - see DrumsPlayer.OnPadHit). Rather than the dedicated Miss Animator
        /// trigger (which reads as far too subtle/invisible on Circular's shared controller),
        /// this reuses the exact same Hit/CymbalHit trigger a genuine hit gets (see
        /// DrumsPlayer.AnimateFret) so the player still gets clear movement feedback on
        /// whichever pad they actually pressed - but deliberately skips the HitEffect flame
        /// burst a real hit gets (<paramref name="isCymbal"/> only picks which Animator trigger
        /// to use, never calls PlayHitParticles) and, via <paramref name="playParticles"/>,
        /// optionally the separate MissEffect indicator particle too - so an overhit still reads
        /// as clearly distinct from a correct hit.
        /// </summary>
        public void PlayOverhitAnimation(int index, bool isCymbal, bool playParticles)
        {
            if (!_frets.TryGetValue(index, out var fret))
            {
                return;
            }

            if (isCymbal)
            {
                fret.PlayCymbalHitAnimation();
            }
            else
            {
                fret.PlayHitAnimation();
            }

            if (playParticles)
            {
                fret.PlayMissParticles();
            }
        }

        /// <summary>
        /// Dimly lights up (or turns off) a single fret's inner part - see
        /// Fret.SetOverhitPressed. Driven every frame by DrumsPlayer's own continuous
        /// time-tracking (mirroring SetPressedImpulse's own correct-hit light-up), for a
        /// tom/cymbal overhit (wrong pad) or wrong-timing kick hit.
        /// </summary>
        public void SetOverhitPressed(int index, bool pressed)
        {
            if (_frets.TryGetValue(index, out var fret))
            {
                fret.SetOverhitPressed(pressed);
            }
        }

        public void PlayFullWidthMissAnimation()
        {
            foreach (var (_, fret) in _frets)
            {
                fret.PlayOpenMissAnimation();
                fret.PlayOpenMissParticles();
            }
        }

        public void PlayKickFretAnimation()
        {
            foreach (var kick in _kickFrets)
            {
                kick.PlayHitAnimation();
            }
        }

        /// <summary>
        /// Makes every regular button fret jump (the same per-pad hit animation used for a
        /// normal note hit), without any light-up/particle burst. Used by Circular's kick
        /// animation on its own - both for a correct kick hit (layered under the full
        /// PlayKickButtonBurst below) and, on its own, for a wrong-timing kick overhit, which
        /// should show the same jump but skip the light-up/particles a correct hit gets.
        /// </summary>
        public void PlayKickButtonJump()
        {
            foreach (var (_, fret) in _frets)
            {
                fret.PlayHitAnimation();
                fret.PlayCymbalHitAnimation();
            }
        }

        /// <summary>
        /// Makes every regular button fret jump (see PlayKickButtonJump above) and briefly flash
        /// its own (dimmed/flattened) Sustain Effect. Used by Circular's kick animation to give
        /// a whole-kit flash across the row, on top of the dedicated kick lane/flash arc.
        /// Triggers both the tom and cymbal hit animations - a given fret only has one of the
        /// two model variants active at a time (see Player.Profile.UseCymbalModels), so this
        /// makes every fret jump regardless of which variant it's currently showing, without
        /// FretArray needing to track each index's configured pad type.
        /// </summary>
        public void PlayKickButtonBurst(float duration)
        {
            PlayKickButtonJump();

            foreach (var (_, fret) in _frets)
            {
                fret.PlayTimedEffectBurst(fret.ThemeBind.SustainEffect, duration);
            }
        }

        /// <summary>
        /// Combined world-space bounds of every button's own visible mesh(es). Used by Circular
        /// to position its kick flash from the buttons' actual rendered geometry instead of a
        /// guessed offset, since "underneath the buttons" means something different depending on
        /// each theme's authored button scale/pivot, which this sidesteps entirely.
        /// </summary>
        public Bounds GetFretButtonWorldBounds()
        {
            var bounds = new Bounds();
            var hasBounds = false;

            foreach (var (_, fret) in _frets)
            {
                // MeshRenderer only - deliberately excludes ParticleSystemRenderer (the hit/
                // sustain effect particles also living under the same fret), whose bounds only
                // cover currently-alive particles and would otherwise skew/degenerate the result
                // when idle, since no effect is playing while this is being measured at init.
                foreach (var renderer in fret.GetComponentsInChildren<MeshRenderer>())
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
            }

            return bounds;
        }

        /// <summary>
        /// The material used by one of the fret's own hit-effect particles, for reuse as a
        /// known-good template by other runtime-created effects (e.g. Circular's kick ring
        /// pulse). This guarantees an already-correctly-configured transparent/glowing shader
        /// setup for whatever theme is active, instead of guessing at a brand new material/
        /// shader from scratch with no Editor access to verify it renders as intended.
        /// </summary>
        public Material GetHitEffectTemplateMaterial()
        {
            foreach (var (_, fret) in _frets)
            {
                foreach (var particle in fret.ThemeBind.HitEffect.EffectParticles)
                {
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        return renderer.sharedMaterial;
                    }
                }
            }

            return null;
        }

        public void ResetAll()
        {
            foreach (var (_, fret) in _frets)
            {
                fret.SetSustained(false);
                fret.SetBreMode(false);
            }
        }

        public void UpdateAccentColorState(int fretIndex, bool shouldWhiten)
        {
            if (shouldWhiten)
            {
                _frets[fretIndex].WhitenFretColor();
            }
            else
            {
                _frets[fretIndex].RestoreFretColor();
            }
        }

        public void SetFretColorPulse(int fretIndex, bool pulse, float duration)
        {
            _pulseDuration = duration;

            if (pulse)
            {
                _pulsingFrets.Add(fretIndex);
            }
            else
            {
                _pulsingFrets.Remove(fretIndex);
            }
        }

        public void PulseFretColors()
        {
            foreach (var fretIndex in _frets.Keys)
            {
                if (!_pulsingFrets.Contains(fretIndex) || _activeFrets.Contains(fretIndex))
                {
                    continue;
                }

                _frets[fretIndex].FadeColor(_pulseDuration, true, false);
            }
        }

        public void UpdateFretActiveState(List<int> newActiveFrets)
        {
            foreach (var fretIndex in _frets.Keys)
            {
                if (_activeFrets.Contains(fretIndex) != newActiveFrets.Contains(fretIndex))
                {
                    if (newActiveFrets.Contains(fretIndex))
                    {
                        _frets[fretIndex].ResetColor(true);
                        _activeFrets.Add(fretIndex);
                    }
                    else
                    {
                        _frets[fretIndex].DimColor(true);
                        _activeFrets.Remove(fretIndex);
                    }
                }
            }
        }

        public void SetBreMode(bool breMode)
        {
            foreach (var (_, fret) in _frets)
            {
                fret.SetBreMode(breMode);
            }
        }
    }
}