using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;
using static YARG.Themes.ThemeManager;

namespace YARG.Gameplay.Visuals
{
    public readonly struct HighwayOrderingInfo
    {
        public HighwayOrderingInfo(float position, int colorIndex)
        {
            Position = position;
            ColorIndex = colorIndex;
        }

        public float Position { get; }
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

        private List<int> _activeFrets = new();
        private List<int> _pulsingFrets = new();
        private float  _pulseDuration;

        /*
         * Overload for instruments where lefty flip does not affect color (e.g. a lefty-flipped Green Fret on 5F Guitar is still green, just laterally shifted).
         * Acts as a convenience so that you can just pass in a mapping of note type to lateral position, and it will create HighwayOrderingInfos that use the note
         * type as the color index.
         *
         * On Drums, lefty flip affects position and color separately (e.g. a lefty flipped Red Drum becomes green in addition to being shifted), so you need to
         * provide HighwayOrderingInfos directly.
         */
        public void Initialize(Dictionary<int, int> highwayOrdering, int laneCount, GameObject? kickFretPrefab, IFretColorProvider fretColorProvider, ThemePreset themePreset, VisualStyle style)
        {
            var derivedDictionary = new Dictionary<int, HighwayOrderingInfo>();

            foreach (var (noteType, position) in highwayOrdering)
            {
                derivedDictionary.Add(noteType, new(position, noteType));
            }

            Initialize(derivedDictionary, laneCount, kickFretPrefab, fretColorProvider, themePreset, style);
        }

        public void Initialize(Dictionary<int, HighwayOrderingInfo> highwayOrdering, int laneCount, GameObject? kickFretPrefab, IFretColorProvider fretColorProvider, ThemePreset themePreset, VisualStyle style)
        {
            var fretPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, style);

            _frets.Clear();
            foreach (var (noteType, highwayOrderingInfo) in highwayOrdering)
            {
                var fret = Instantiate(fretPrefab, transform);
                fret.SetActive(true);


                // Position
                float x = _trackWidth / laneCount * highwayOrderingInfo.Position - _trackWidth / 2f + 1f / laneCount;
                fret.transform.localPosition = new Vector3(x, 0f, 0f);

                // Scale
                float scale = (_trackWidth / WIDTH_NUMERATOR) / (laneCount / WIDTH_DENOMINATOR);
                fret.transform.localScale = new Vector3(scale, 1f, 1f);

                var fretComp = fret.GetComponent<Fret>();
                fretComp.Initialize(
                    fretColorProvider.GetFretColor(highwayOrderingInfo.ColorIndex),
                    fretColorProvider.GetFretInnerColor(highwayOrderingInfo.ColorIndex),
                    fretColorProvider.GetParticleColor(highwayOrderingInfo.ColorIndex),
                    fretColorProvider.GetParticleColor((int)FiveFretGuitarFret.Open)
                );

                _frets[noteType] = fretComp;
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

        public void SetPressed(int index, bool pressed)
        {
            _frets[index].SetPressed(pressed);
        }

        public void SetPressedDrum(int index, bool pressed, Fret.AnimType animType)
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

        public void PlayCodaHitAnimation(int index)
        {
            // index = Mathf.Clamp(index, _frets.Keys.Min(), _frets.Keys.Max());
            _frets[index].PlayHitAnimation();
            _frets[index].PlayHitParticles();
        }

        public void PlayCymbalHitAnimation(int index)
        {
            _frets[index].PlayCymbalHitAnimation();
            _frets[index].PlayHitParticles();
        }

        public void PlayOpenHitAnimation()
        {
            foreach (var (_, fret) in _frets)
            {
                fret.PlayHitAnimation();
                fret.PlayOpenHitParticles();
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

        public void PlayOpenMissAnimation()
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