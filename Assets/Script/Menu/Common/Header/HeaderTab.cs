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

        private HeaderTabs _headerTabs;
        private string _id;

        public void Init(HeaderTabs headerTabs, string id, string displayName, Sprite sprite)
        {
            _headerTabs = headerTabs;
            _id = id;

            _displayName.text = displayName;
            _sprite.sprite = sprite;
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (!selected) return;

            _headerTabs.SelectedTabId = _id;
        }
    }
}