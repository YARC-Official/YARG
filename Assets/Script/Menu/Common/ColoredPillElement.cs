using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu
{
    public class ColoredPillElement : MonoBehaviour
    {
        public enum ColoredPillPreset
        {
            Default,
            CasualEngine,
            PrecisionEngine,
            CustomEngine,
            EasierModifier,
            HarderModifier,
            NeutralModifier
        }

        [SerializeField]
        private TextMeshProUGUI _textBox;
        [SerializeField]
        private Image _backgroundImage;
        [SerializeField]
        private Image _outlineImage;

        [Space]
        [SerializeField]
        private Color[] _textColors;
        [SerializeField]
        private Color[] _backgroundColors;
        [SerializeField]
        private Color[] _outlineColors;

        public void SetValues(string text, ColoredPillPreset preset)
        {
            _textBox.text = text;
            SetPresetColors(preset);
        }

        public void SetPresetColors(ColoredPillPreset preset)
        {
            int idx = (int) preset;

            _textBox.color = _textColors[idx];
            _backgroundImage.color = _backgroundColors[idx];
            _outlineImage.color = _outlineColors[idx];
        }
    }
}