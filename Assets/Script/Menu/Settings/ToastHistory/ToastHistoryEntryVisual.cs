using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Settings.ToastHistory
{
    public class ToastHistoryEntryVisual : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField]
        private GameObject _evenBackground;

        public void Initialize(string text, Color color, Sprite icon)
        {
            _text.text = text;
            _text.color = color;
            _icon.sprite = icon;
        }

        public void AssignIndex(int index)
        {
            if (_evenBackground != null)
            {
                _evenBackground.SetActive(index % 2 == 0);
            }
        }
    }
}
