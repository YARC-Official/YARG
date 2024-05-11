using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
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

        public void Initialize(ThemePreset themePreset, GameMode gameMode,
            ColorProfile.IFretColorProvider fretColorProvider, bool leftyFlip)
        {
            var fretPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(
                themePreset, gameMode);

            // Spawn in normal frets
            _frets.Clear();
            for (int i = 0; i < FretCount; i++)
            {
                // Spawn
                var fret = Instantiate(fretPrefab, transform);
                fret.SetActive(true);

                // Position
                float x = _trackWidth / FretCount * i - _trackWidth / 2f + 1f / FretCount;
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

            InitializeColor(fretColorProvider, leftyFlip);
        }

        public void InitializeColor(ColorProfile.IFretColorProvider fretColorProvider, bool leftyFlip)
        {
            for (int i = 0; i < _frets.Count; i++)
            {
                int index = i + 1;
                if (DontFlipColorsLeftyFlip && leftyFlip)
                {
                    index = _frets.Count - index + 1;
                }

                _frets[i].Initialize(
                    fretColorProvider.GetFretColor(index),
                    fretColorProvider.GetFretInnerColor(index),
                    fretColorProvider.GetParticleColor(index));
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
            }
        }

        public void PlayDrumAnimation(int index, bool particles)
        {
            _frets[index].PlayHitAnimation();

            if (particles)
            {
                _frets[index].PlayHitParticles();
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
    }
}