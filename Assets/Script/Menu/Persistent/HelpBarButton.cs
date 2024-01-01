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
        private TextMeshProUGUI _text;

        private NavigationScheme.Entry? _entry;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;

            _text.text = entry.DisplayName;

            // Set the icon
            var icons = MenuData.NavigationIcons;
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonImage.color = icons.GetColor(entry.Action);
            _buttonBackground.color = new Color32(255, 255, 255, 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonBackground.color = new Color32(255, 255, 255, 2);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonBackground.color = new Color32(255, 255, 255, 0);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _buttonBackground.color = new Color32(255, 255, 255, 5);
        }

        public void OnClick()
        {
            _entry?.Invoke();
        }


    }
}