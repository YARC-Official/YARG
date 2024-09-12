using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.ProGuitar;
using YARG.Core.Logging;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public sealed class ProGuitarNoteElement : TrackElement<ProGuitarPlayer>
    {
        public const int HEIGHT_DEFAULT = 2;
        public const int HEIGHT_VARIATIONS = 6;

        public ProGuitarNote ChordRef { get; set; }

        public override double ElementTime => ChordRef.Time;

        [SerializeField]
        private TextMeshPro[] _textObjects;

        [SerializeField]
        private GameObject _fretObjectsParent;
        [SerializeField]
        private ProGuitarSingleFret[] _fretObjects;

        private void Start()
        {
            YargLogger.Assert(_textObjects.Length == 6);
            YargLogger.Assert(_fretObjects.Length == 6);
        }

        protected override void InitializeElement()
        {
            var mask = ChordRef.ChordMask;

            // Find the lowest and highest values
            byte? min = null;
            byte? max = null;
            for (int i = 1; i < 6; i++)
            {
                var fret = mask[i];
                if (fret == FretBytes.IGNORE_BYTE)
                {
                    continue;
                }

                if (max is null || fret > max)
                {
                    max = fret;
                }

                if (min is null || fret < min)
                {
                    min = fret;
                }
            }

            // If a maximum can't be found, that means there are no notes in the chord!
            if (max is null)
            {
                YargLogger.LogWarning("The chord has no notes!");
                return;
            }

            _fretObjectsParent.SetActive(true);

            // Display each part of the chord
            for (int i = 0; i < 6; i++)
            {
                var fret = mask[i];

                // If the fret is an ignore byte, it means no note is charted in that position
                if (fret == FretBytes.IGNORE_BYTE)
                {
                    _textObjects[i].gameObject.SetActive(false);
                    _fretObjects[i].gameObject.SetActive(false);
                    continue;
                }

                _textObjects[i].gameObject.SetActive(true);
                _fretObjects[i].gameObject.SetActive(true);

                _textObjects[i].text = ZString.Format("{0}", fret);

                // Find the height of the fret in the chord
                int height;
                if (max == min)
                {
                    height = HEIGHT_DEFAULT;
                }
                else
                {
                    // Find the percent of the height relative to the min and max fret values of the chord
                    float percent = (float) (fret - min.Value) / (max.Value - min.Value);
                    height = Mathf.RoundToInt((HEIGHT_VARIATIONS - 1) * percent);
                }
                _fretObjects[i].Initialize(height);
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
            _fretObjectsParent.SetActive(false);

            foreach (var text in _textObjects)
            {
                text.gameObject.SetActive(false);
            }
        }

        public void HitNote()
        {
            HideElement();
        }
    }
}