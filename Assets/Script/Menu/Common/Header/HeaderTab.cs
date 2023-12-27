using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu
{
    public class HeaderTab : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _displayName;
        [SerializeField]
        private Image _sprite;

        public string Id { get; private set; }

        public void Init(string id, string displayName, Sprite sprite)
        {
            Id = id;

            _displayName.text = displayName;
            _sprite.sprite = sprite;
        }
    }
}