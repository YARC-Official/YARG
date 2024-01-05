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

        private Color32 ButtonBackgroundColor;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;
            var icons = MenuData.NavigationIcons;
            ButtonBackgroundColor = icons.GetColor(entry.Action);

            _buttonLabel.text = entry.DisplayName;
            _buttonLabel.color = Color.white;

            _buttonText.color = Color.white;

            // Set the icon color
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = ButtonBackgroundColor;
            _buttonBackground.color = new Color32(255, 255, 255, 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonBackground.color = ButtonBackgroundColor;
            _buttonImage.color = ButtonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonBackground.color = new Color32(255, 255, 255, 0);
            _buttonImage.color = ButtonBackgroundColor;
            _buttonLabel.color = Color.white;
            _buttonText.color = Color.white;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = new Color32(75, 75, 75, 255);
            _buttonImage.color = new Color32(100, 100, 100, 255);
            _buttonLabel.color = new Color32(100, 100, 100, 255);
            _buttonText.color = new Color32(255, 255, 255, 25);
        }

        public void OnClick()
        {
            _entry?.Invoke();
        }


    }
}