using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    public class ShowPickerButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        private Image _buttonImage;
        [SerializeField]
        private Image _buttonBackground;
        [SerializeField]
        public TextMeshProUGUI ButtonText;

        [SerializeField]
        private Button _button;

        [Space]
        [SerializeField]
        private SongPickerListDialog _dialog;

        private Image _backgroundImage;

        private NavigationScheme.Entry? _entry;

        private Color _buttonBackgroundColor;

        private void OnEnable()
        {
            _buttonBackgroundColor = _buttonImage.color;
            _buttonBackground.color = Color.clear;
        }

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;
            var icons = MenuData.NavigationIcons;
            _buttonBackgroundColor = icons.GetColor(entry.Action);

            // Show/hide text and transitions
            var special = entry.Action is MenuAction.Select or MenuAction.Start;
            _button.transition = special
                ? Selectable.Transition.None
                : Selectable.Transition.SpriteSwap;

            // Set colors
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = _buttonBackgroundColor;
            _buttonBackground.color = Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColor;
            _buttonImage.color = _buttonBackgroundColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonBackground.color = Color.clear;
            _buttonImage.color = _buttonBackgroundColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = Color.grey;
            _buttonImage.color = Color.grey;

            _entry?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _entry?.InvokeHoldOffHandler();
        }
    }
}