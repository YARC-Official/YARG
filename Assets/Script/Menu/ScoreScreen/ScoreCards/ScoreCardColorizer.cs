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

        [SerializeField]
        private Image[] _coloredImages;
        [SerializeField]
        private TextMeshProUGUI[] _coloredHeaders;

        [Space]
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Image _bottomTag;

        [Space]
        [SerializeField]
        private Sprite[] _backgrounds;
        [SerializeField]
        private Sprite[] _tags;

        [Space]
        [SerializeField]
        private Color[] _colors;
        [SerializeField]
        private Color[] _headerColors;

        private ScoreCardColor _scoreCardColor;

        public Color CurrentColor => _colors[(int) _scoreCardColor];
        public Color HeaderColor => _headerColors[(int) _scoreCardColor];

        public void SetCardColor(ScoreCardColor scoreCardColor)
        {
            _scoreCardColor = scoreCardColor;

            foreach (var image in _coloredImages)
            {
                image.color = CurrentColor;
            }

            foreach (var text in _coloredHeaders)
            {
                text.color = HeaderColor;
            }

            _background.sprite = _backgrounds[(int) scoreCardColor];
            _bottomTag.sprite = _tags[(int) scoreCardColor];
        }
    }
}