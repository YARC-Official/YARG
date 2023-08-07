using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    public class HelpBarButton : MonoBehaviour
    {
        [SerializeField]
        private Image _buttonImage;

        [SerializeField]
        private TextMeshProUGUI _text;

        private NavigationScheme.Entry? _entry;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry)
        {
            _entry = entry;

            _text.text = entry.DisplayName;

            // Set the icon
            var icons = GlobalVariables.Instance.MenuIcons;
            _buttonImage.sprite = icons.GetIcon(entry.Type);
            _buttonImage.color = icons.GetColor(entry.Type);
        }

        public void OnClick()
        {
            _entry?.Func();
        }
    }
}