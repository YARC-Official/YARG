using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Game;

namespace YARG.Gameplay.Visuals
{
    public class FretArray : MonoBehaviour
    {
        private const float WIDTH_NUMERATOR   = 2f;
        private const float WIDTH_DENOMINATOR = 5f;

        [FormerlySerializedAs("_fretCount")]
        public int FretCount;
        [SerializeField]
        private float _trackWidth = 2f;

        private GameObject _fretPrefab;

        private readonly List<Fret> _frets = new();

        public IReadOnlyList<Fret> Frets => _frets;

        public void Initialize(GameObject fretPrefab, ColorProfile.IFretColorProvider fretColorProvider, bool leftyFlip)
        {
            _fretPrefab = fretPrefab;

            _frets.Clear();
            for (int i = 0; i < FretCount; i++)
            {
                // Spawn
                var fret = Instantiate(_fretPrefab, transform);
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

            InitializeColor(fretColorProvider);
        }

        public void InitializeColor(ColorProfile.IFretColorProvider fretColorProvider)
        {
            for (int i = 0; i < _frets.Count; i++)
            {
                _frets[i].Initialize(
                    fretColorProvider.GetFretColor(i + 1),
                    fretColorProvider.GetFretInnerColor(i + 1),
                    fretColorProvider.GetParticleColor(i + 1));
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
            _frets[index].PlayDrumAnimation();

            if (particles)
            {
                _frets[index].PlayHitParticles();
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