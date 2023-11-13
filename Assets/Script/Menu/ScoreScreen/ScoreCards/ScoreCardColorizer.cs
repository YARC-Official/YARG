using System;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.ScoreScreen
{
    public class ScoreCardColorizer : MonoBehaviour
    {
        public enum ScoreCardColor
        {
            Blue,
            Gold,
            Red,
            Gray
        }

        [SerializeField]
        private Image[] _coloredImages;
        [SerializeField]
        private Image _background;

        [Space]
        [SerializeField]
        private Sprite _blueBackground;
        [SerializeField]
        private Sprite _goldBackground;
        [SerializeField]
        private Sprite _redBackground;
        [SerializeField]
        private Sprite _grayBackground;

        [Space]
        [SerializeField]
        private Color _blueColor;
        [SerializeField]
        private Color _goldColor;
        [SerializeField]
        private Color _redColor;
        [SerializeField]
        private Color _grayColor;

        private ScoreCardColor _scoreCardColor;

        public Color CurrentColor => _scoreCardColor switch
        {
            ScoreCardColor.Blue => _blueColor,
            ScoreCardColor.Gold => _goldColor,
            ScoreCardColor.Red  => _redColor,
            ScoreCardColor.Gray => _grayColor,
            _                   => throw new Exception("Unreachable.")
        };

        public void SetCardColor(ScoreCardColor scoreCardColor)
        {
            _scoreCardColor = scoreCardColor;

            _background.sprite = scoreCardColor switch
            {
                ScoreCardColor.Blue => _blueBackground,
                ScoreCardColor.Gold => _goldBackground,
                ScoreCardColor.Red  => _redBackground,
                ScoreCardColor.Gray => _grayBackground,
                _                   => throw new Exception("Unreachable.")
            };

            foreach (var image in _coloredImages)
            {
                image.color = CurrentColor;
            }
        }
    }
}