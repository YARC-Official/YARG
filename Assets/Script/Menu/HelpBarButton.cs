using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.UI
{
    public class HelpBarButton : MonoBehaviour
    {
        [SerializeField]
        private Image _buttonImage;

        [SerializeField]
        private TextMeshProUGUI _text;

        private NavigationScheme.Entry? _entry;

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry, Color c)
        {
            _entry = entry;

            _text.text = entry.DisplayName;
            _buttonImage.color = c;
        }

        public void OnClick()
        {
            _entry?.Func();
        }
    }
}