using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.ScoreScreen
{
    public class ScoreCardColorizer : MonoBehaviour
    {
        public enum ScoreCardColor
        {
            Blue = 0,
            Gold = 1,
            Red  = 2,
            Gray = 3
        }

        [Space]
        [SerializeField]
        private Image[] _coloredImages;
        [SerializeField]
        private Color[] _colors;

        [Space]
        [SerializeField]
        private Image _headerBackgroundGradient;

        [Space]
        [SerializeField]
        private TextMeshProUGUI[] _coloredHeaders;
        [SerializeField]
        private Color[] _headerColors;

        [Space]
        [SerializeField]
        private TextMeshProUGUI[] _coloredTextFields;
        [SerializeField]
        private Color[] _coloredTextColors;

        [Space]
        [SerializeField]
        private Image _headerTag;
        [SerializeField]
        private Sprite[] _headerTags;

        [Space]
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Sprite[] _backgrounds;

        [Space]
        [SerializeField]
        private Image _bottomTag;
        [SerializeField]
        private Sprite[] _tags;

        [Space]
        [SerializeField]
        private Image[] _borderImages;
        [SerializeField]
        private Sprite[] _borders;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _bottomTagText;
        [SerializeField]
        private Color[] _bottomTagTextColors;

        private ScoreCardColor _scoreCardColor;

        public Color CurrentColor => _colors[(int) _scoreCardColor];
        public Color HeaderColor => _headerColors[(int) _scoreCardColor];

        public void SetCardColor(ScoreCardColor scoreCardColor)
        {
            _scoreCardColor = scoreCardColor;
            int idx = (int) scoreCardColor;

            foreach (var image in _coloredImages)
            {
                image.color = CurrentColor;
            }

            foreach (var text in _coloredHeaders)
            {
                text.color = HeaderColor;
            }

            foreach (var text in _coloredTextFields)
            {
                text.color = _coloredTextColors[idx];
            }

            _background.sprite = _backgrounds[idx];
            _headerTag.sprite = _headerTags[idx];
            _bottomTag.sprite = _tags[idx];

            foreach (var border in _borderImages)
            {
                border.sprite = _borders[idx];
            }

            _bottomTagText.color = _bottomTagTextColors[idx];

            // with the default blue and gray cards, the instrument icon looks better with
            // an extra gradient overlaid
            _headerBackgroundGradient.enabled = (scoreCardColor != ScoreCardColor.Gold) && (scoreCardColor != ScoreCardColor.Red);
        }
    }
}