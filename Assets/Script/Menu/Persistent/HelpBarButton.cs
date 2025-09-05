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
        private Button _button;

        [SerializeField]
        private TextMeshProUGUI _buttonLabel;
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        private NavigationScheme.Entry? _entry;

        private Color _buttonBackgroundColor;

        private bool _clickable = true;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry, bool clickable = true)
        {
            _clickable = clickable;
            if (clickable)
            {
                _entry = entry;
            }

            var icons = MenuData.NavigationIcons;
            _buttonBackgroundColor = icons.GetColor(entry.Action);

            // Label
            _buttonLabel.text = entry.DisplayName;
            _buttonLabel.color = Color.white;

            // Show/hide text and transitions
            var special = entry.Action is MenuAction.Select or MenuAction.Start or MenuAction.Left or MenuAction.Right;
            _buttonText.gameObject.SetActive(!special);
            _button.transition = special
                ? Selectable.Transition.None
                : Selectable.Transition.SpriteSwap;

            // Set colors
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = _buttonBackgroundColor;
            _buttonBackground.color = Color.clear;
            _buttonText.color = Color.white;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            _buttonBackground.color = _buttonBackgroundColor;
            _buttonImage.color = _buttonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            _buttonBackground.color = Color.clear;
            _buttonImage.color = _buttonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            _buttonBackground.color = Color.grey;
            _buttonImage.color = Color.grey;
            _buttonLabel.color = Color.grey;
            _buttonText.color = Color.grey;

            _entry?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            _entry?.InvokeHoldOffHandler();
        }
    }
}