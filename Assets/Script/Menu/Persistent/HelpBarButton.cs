using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    public class HelpBarButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        private Image _buttonImage;
        [SerializeField]
        private Image _buttonBackground;
        [SerializeField]
        private Image _buttonOutline;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TextMeshProUGUI _buttonLabel;
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        public enum HelpButtonStyle
        {
            Default,
            Filled
        }

        [SerializeField]
        private HelpButtonStyle _buttonStyle;

        private NavigationScheme.Entry? _entry;

        private Color _buttonBackgroundColor;
        private Color _buttonImageColor;
        private Color _buttonBackgroundColorOnHover;
        private Color _buttonBackgroundColorOnDown;

        private readonly Color _coolGrey = new Color(123 / 255f, 127 / 255f, 154 / 255f, 1f);

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;
            var icons = MenuData.NavigationIcons;
            _buttonBackgroundColor = icons.GetColor(entry.Action);
            _buttonBackgroundColor.a = 0.05f;
            _buttonBackgroundColorOnHover = icons.GetColor(entry.Action);
            _buttonBackgroundColorOnHover.a = 0.2f;
            _buttonBackgroundColorOnDown = icons.GetColor(entry.Action);
            _buttonBackgroundColorOnDown.a = 0.1f;
            _buttonImageColor = icons.GetColor(entry.Action);
            _buttonImageColor.a = 1f;

            // Label
            _buttonLabel.text = entry.DisplayName;

            // Show/hide text and transitions
            var special = entry.Action is MenuAction.Select or MenuAction.Start;
            _buttonText.gameObject.SetActive(!special);
            _button.transition = special
                ? Selectable.Transition.None
                : Selectable.Transition.SpriteSwap;

            // Set colors
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = _buttonImageColor;
            if (_buttonStyle == HelpButtonStyle.Default)
            {
                _buttonBackground.color = Color.clear;
                _buttonOutline.color = Color.clear;
                _buttonLabel.color = _coolGrey;
                _buttonText.color = _coolGrey;
            }
            else
            {
                _buttonBackground.color = _buttonBackgroundColorOnHover;
                _buttonOutline.color = _buttonBackgroundColorOnHover;
                _buttonLabel.color = Color.white;
                _buttonText.color = Color.white;
            }

        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonImage.color = _buttonImageColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
            if (_buttonStyle == HelpButtonStyle.Default)
            {
                _buttonBackground.color = _buttonBackgroundColor;
                _buttonOutline.color = _buttonBackgroundColor;
            }
            else
            {
                _buttonBackground.color = _buttonBackgroundColorOnHover;
                _buttonOutline.color = _buttonBackgroundColorOnHover;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonImage.color = _buttonImageColor;
            if (_buttonStyle == HelpButtonStyle.Default)
            {
                _buttonBackground.color = Color.clear;
                _buttonOutline.color = Color.clear;
                _buttonLabel.color = _coolGrey;
                _buttonText.color = _coolGrey;
            }
            else
            {
                _buttonBackground.color = _buttonBackgroundColorOnHover;
                _buttonOutline.color = _buttonBackgroundColorOnHover;
                _buttonLabel.color = Color.white;
                _buttonText.color = Color.white;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColorOnDown;
            _buttonImage.color = _buttonImageColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
            _buttonOutline.color = _buttonBackgroundColor;

            _entry?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _buttonImage.color = _buttonImageColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
            if (_buttonStyle == HelpButtonStyle.Default)
            {
                _buttonBackground.color = Color.clear;
                _buttonOutline.color = Color.clear;
            }
            else
            {
                _buttonBackground.color = _buttonBackgroundColor;
                _buttonOutline.color = _buttonBackgroundColor;
            }

            _entry?.InvokeHoldOffHandler();
        }
    }
}