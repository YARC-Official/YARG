using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YARG.Menu.Data;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    public class HelpBarButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField]
        private Image _buttonImage;

        [SerializeField]
        private Image _buttonBackground;

        [SerializeField]
        private TextMeshProUGUI _buttonLabel;

        [SerializeField]
        private TextMeshProUGUI _buttonText;

        private NavigationScheme.Entry? _entry;

        private Color _buttonBackgroundColor;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;
            var icons = MenuData.NavigationIcons;
            _buttonBackgroundColor = icons.GetColor(entry.Action);

            _buttonLabel.text = entry.DisplayName;
            _buttonLabel.color = Color.white;

            _buttonText.color = Color.white;

            // Set the icon color
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = _buttonBackgroundColor;
            _buttonBackground.color = Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonBackground.color = _buttonBackgroundColor;
            _buttonImage.color = _buttonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonBackground.color = Color.clear;
            _buttonImage.color = _buttonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = Color.grey;
            _buttonImage.color = Color.grey;
            _buttonLabel.color = Color.grey;
            _buttonText.color = Color.grey;
        }

        public void OnClick()
        {
            _entry?.Invoke();
        }


    }
}