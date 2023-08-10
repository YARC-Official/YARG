using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Game;

namespace YARG.Gameplay.Visuals
{
    public class FretArray : MonoBehaviour
    {
        private const float WIDTH_NUMERATOR   = 2f;
        private const float WIDTH_DENOMINATOR = 5f;

        [SerializeField]
        private int _fretCount;
        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private GameObject _fretPrefab;

        private readonly List<Fret> _frets = new();

        public IReadOnlyList<Fret> Frets => _frets;

        public void Initialize(ColorProfile colorProfile, bool leftyFlip)
        {
            _frets.Clear();
            for (int i = 0; i < _fretCount; i++)
            {
                var fret = Instantiate(_fretPrefab, transform);

                // Position
                float x = _trackWidth / _fretCount * i - _trackWidth / 2f + 1f / _fretCount;
                fret.transform.localPosition = new(leftyFlip ? -x : x, 0f, 0f);

                // Scale
                float scale = (_trackWidth / WIDTH_NUMERATOR) / (_fretCount / WIDTH_DENOMINATOR);
                fret.transform.localScale = new(scale, 1f, 1f);

                // Add
                var fretComp = fret.GetComponent<Fret>();
                _frets.Add(fretComp);

                // Color
                var colors = colorProfile.FiveFretGuitar;
                fretComp.Initialize(
                    colors.GetFretColor(i + 1),
                    colors.GetFretInnerColor(i + 1),
                    colors.GetNoteParticleColor(i + 1));
            }
        }

        public void SetPressed(int index, bool pressed)
        {
            _frets[index].SetPressed(pressed);
        }

        public void PlayHitAnimation(int index)
        {
            _frets[index].PlayHitAnimation();
        }

        public void PlayOpenHitAnimation()
        {
            foreach (var fret in _frets)
            {
                fret.PlayHitAnimation();
            }
        }
    }
}