using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.MusicLibrary
{
    public class HoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        private Image _buttonImage;
        [SerializeField]
        private Image _buttonBackground;
        [SerializeField]
        private Image _buttonOutline;

        [SerializeField]
        private Button _button;

        private Color _buttonBackgroundColor;
        private Color _buttonBackgroundColorOnHover;
        private Color _buttonBackgroundColorOnDown;

        private Action _onClickHandler;

        private readonly Color _coolGrey = new Color(123 / 255f, 127 / 255f, 154 / 255f, 1f);

        public void Initialize(Action onClickHandler)
        {
            _buttonBackgroundColor = _buttonBackground.color.WithAlpha(0.3f);
            _buttonBackgroundColorOnHover = _buttonBackground.color.WithAlpha(0.4f);
            _buttonBackgroundColorOnDown = _buttonBackground.color.WithAlpha(0.1f);

            _button.transition = Selectable.Transition.None;

            // Set colors
            _buttonBackground.color = _buttonBackgroundColor;
            _buttonOutline.color = _buttonBackgroundColor;

            _onClickHandler = onClickHandler;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColorOnHover;
            _buttonOutline.color = _buttonBackgroundColorOnHover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColor;
            _buttonOutline.color = _buttonBackgroundColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColorOnDown;
            _buttonOutline.color = _buttonBackgroundColor;

            _onClickHandler.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColorOnHover;
            _buttonOutline.color = _buttonBackgroundColorOnHover;
        }
    }
}