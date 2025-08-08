using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class FretArray : MonoBehaviour
    {
        private const float WIDTH_NUMERATOR   = 2f;
        private const float WIDTH_DENOMINATOR = 5f;

        public int FretCount;
        public bool DontFlipColorsLeftyFlip;
        public bool UseKickFrets;

        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private Transform _leftKickFretPosition;
        [SerializeField]
        private Transform _rightKickFretPosition;

        private readonly List<Fret> _frets = new();
        private readonly List<KickFret> _kickFrets = new();

        private bool[] _activeFrets;
        private bool[] _pulsingFrets;
        private float  _pulseDuration;

        public void Initialize(ThemePreset themePreset, GameMode gameMode,
            ColorProfile.IFretColorProvider fretColorProvider, bool leftyFlip, bool splitProTomsAndCymbals, bool swapSnareAndHiHat, bool swapCrashAndRide)
        {
            var fretPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(
                themePreset, gameMode);

            // Spawn in normal frets
            _frets.Clear();
            for (int i = 0; i < FretCount; i++)
            {
                int effectivePosition = i switch
                {
                    0 => swapSnareAndHiHat ? 1 : 0,
                    1 => swapSnareAndHiHat ? 0 : 1,
                    3 => swapCrashAndRide ? 5 : 3,
                    5 => swapCrashAndRide ? 3 : 5,
                    _ => i
                };

                // Spawn
                var fret = Instantiate(fretPrefab, transform);
                fret.SetActive(true);

                // Position
                float x = _trackWidth / FretCount * effectivePosition - _trackWidth / 2f + 1f / FretCount;
                fret.transform.localPosition = new Vector3(leftyFlip ? -x : x, 0f, 0f);

                // Scale
                float scale = (_trackWidth / WIDTH_NUMERATOR) / (FretCount / WIDTH_DENOMINATOR);
                fret.transform.localScale = new Vector3(scale, 1f, 1f);

                // Add
                var fretComp = fret.GetComponent<Fret>();
                _frets.Add(fretComp);
            }

            _kickFrets.Clear();
            if (UseKickFrets)
            {
                var kickFretPrefab = ThemeManager.Instance.CreateKickFretPrefabFromTheme(
                    themePreset, gameMode);

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

            InitializeColor(fretColorProvider, leftyFlip, splitProTomsAndCymbals);

            _activeFrets = new bool[FretCount];
            _pulsingFrets = new bool[FretCount];
            // Start with all frets active, they will be set inactive once TrackPlayer figures itself out
            for (int i = 0; i < FretCount; i++)
            {
                _activeFrets[i] = true;
            }
        }

        public void InitializeColor(ColorProfile.IFretColorProvider fretColorProvider, bool leftyFlip, bool splitProTomsAndCymbals)
        {
            for (int i = 0; i < _frets.Count; i++)
            {
                // This needs unique lefty flip logic because it's the one case where
                // the fret order is different from the color profile order
                int index;
                if (splitProTomsAndCymbals)
                {
                    index = i switch
                    {
                        0 => leftyFlip ? 4 : 1,
                        1 => leftyFlip ? 7 : 6,
                        2 => leftyFlip ? 3 : 2,
                        3 => leftyFlip ? 6 : 7,
                        4 => leftyFlip ? 2 : 3,
                        5 => leftyFlip ? 5 : 8,
                        6 => leftyFlip ? 1 : 4,
                        _ => throw new Exception("Unreachable.")
                    };
                }
                else
                {
                    index = i + 1;
                }

                if (DontFlipColorsLeftyFlip && leftyFlip && !splitProTomsAndCymbals)
                {
                    index = _frets.Count - index + 1;
                }

                _frets[i].Initialize(
                    fretColorProvider.GetFretColor(index),
                    fretColorProvider.GetFretInnerColor(index),
                    fretColorProvider.GetParticleColor(index),
                    fretColorProvider.GetParticleColor(0 /* open note */)
                );
            }

            foreach (var kick in _kickFrets)
            {
                kick.Initialize(fretColorProvider.GetFretColor(0));
            }
        }

        public void SetPressed(int index, bool pressed)
        {
            _frets[index].SetPressed(pressed);
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

        public void PlayOpenHitAnimation()
        {
            foreach (var fret in _frets)
            {
                fret.PlayHitAnimation();
                fret.PlayOpenHitParticles();
            }
        }

        public void PlayMissAnimation(int index)
        {
            _frets[index].PlayMissAnimation();
            _frets[index].PlayMissParticles();
        }

        public void PlayOpenMissAnimation()
        {
            foreach (var fret in _frets)
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
            foreach (var fret in _frets)
            {
                fret.SetSustained(false);
            }
        }

        public void SetFretColorPulse(int fretIndex, bool pulse, float duration)
        {
            _pulseDuration = duration;
            _pulsingFrets[fretIndex] = pulse;
        }

        public void PulseFretColors()
        {
            for (int i = 0; i < _pulsingFrets.Length; i++)
            {
                if (!_pulsingFrets[i] || _activeFrets[i])
                {
                    continue;
                }

                _frets[i].FadeColor(_pulseDuration, true, false);
            }
        }

        public void UpdateFretActiveState(bool[] frets)
        {
            // We should always receive the same number of frets that we actually have, but...
            if (frets.Length != _frets.Count)
            {
                YargLogger.LogFormatDebug("Received inconsistent fret array. Got {0} flags, but we have {1} frets.", frets.Length, _frets.Count);
                return;
            }

            for (int i = 0; i < _frets.Count; i++)
            {
                if (_activeFrets[i] != frets[i])
                {
                    if (frets[i])
                    {
                        _frets[i].ResetColor(true);
                    }
                    else
                    {
                        _frets[i].DimColor(true);
                    }
                }

                _activeFrets[i] = frets[i];
            }
        }
    }
}