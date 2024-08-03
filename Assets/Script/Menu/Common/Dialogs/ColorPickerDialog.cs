using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.Dialogs
{
    public class ColorPickerDialog : Dialog
    {
        public enum SliderType
        {
            H = 0, S = 1, V = 2,
            R = 3, G = 4, B = 5
        }

        [Serializable]
        public struct TypeSliderPair
        {
            public SliderType Type;
            public ValueSlider Slider;
        }

        public Color OldColor { get; private set; }

        private Color _newColor;
        public Color NewColor
        {
            get => _newColor;
            private set
            {
                // The color picker does not support transparency
                value.a = 1f;

                _newColor = value;
                UpdateColorImages();
            }
        }

        [SerializeField]
        private Image _oldColorImage;
        [SerializeField]
        private Image _newColorImage;

        [Space]
        [SerializeField]
        private List<TypeSliderPair> _sliders;
        [SerializeField]
        private TMP_InputField _inputField;

        public Action<Color> ColorPickAction;

        private readonly Dictionary<SliderType, ValueSlider> _sliderDict = new();

        private void Awake()
        {
            // Convert slider list into dictionary
            _sliderDict.Clear();
            foreach (var sliderPair in _sliders)
            {
                _sliderDict.Add(sliderPair.Type, sliderPair.Slider);
            }
        }

        protected override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Submit),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Cancel", DialogManager.Instance.ClearDialog)
            }, null);
        }

        public void Initialize(Color initialColor)
        {
            OldColor = initialColor;
            NewColor = initialColor;

            UpdateSliders();
            UpdateTextField();
        }

        private void UpdateSliders()
        {
            // Get color components
            var color = NewColor;
            Color.RGBToHSV(color, out float h, out float s, out float v);

            // Set HSV sliders
            _sliderDict[SliderType.H].SetValueWithoutNotify(h * 255f);
            _sliderDict[SliderType.S].SetValueWithoutNotify(s * 255f);
            _sliderDict[SliderType.V].SetValueWithoutNotify(v * 255f);

            // Set RGB sliders
            _sliderDict[SliderType.R].SetValueWithoutNotify(color.r * 255f);
            _sliderDict[SliderType.G].SetValueWithoutNotify(color.g * 255f);
            _sliderDict[SliderType.B].SetValueWithoutNotify(color.b * 255f);
        }

        private void UpdateTextField()
        {
            _inputField.text = ColorUtility.ToHtmlStringRGB(NewColor);
        }

        private void UpdateColorImages()
        {
            _oldColorImage.color = OldColor;
            _newColorImage.color = NewColor;
        }

        // Unity can't have enums in actions lol
        public void OnSliderChanged(int sliderTypeIndex)
        {
            var sliderType = (SliderType) sliderTypeIndex;

            var color = NewColor;
            Color.RGBToHSV(color, out float h, out float s, out float v);

            switch (sliderType)
            {
                case SliderType.H:
                    h = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.S:
                    s = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.V:
                    v = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.R:
                    color.r = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.G:
                    color.g = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.B:
                    color.b = _sliderDict[sliderType].Value / 255f;
                    break;
                default:
                    throw new Exception("Unreachable.");
            }

            if (sliderType is >= SliderType.H and <= SliderType.V)
            {
                // Update from HSV
                NewColor = Color.HSVToRGB(h, s, v);
            }
            else
            {
                // Update from RGB
                NewColor = color;
            }

            UpdateSliders();
            UpdateTextField();
        }

        public void OnTextFieldChanged()
        {
            // Unity needs a hashtag here, but doesn't put it in when converting to string
            if (ColorUtility.TryParseHtmlString("#" + _inputField.text, out var color))
            {
                NewColor = color;
            }

            UpdateSliders();
            UpdateTextField();
        }

        public override void Submit()
        {
            ColorPickAction?.Invoke(NewColor);

            DialogManager.Instance.ClearDialog();
        }
    }
}