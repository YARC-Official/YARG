using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings
{
    public sealed class CategoryItemView : NavigatableBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private GameObject _currentMarker;

        [SerializeField]
        private CanvasGroup _labelCanvasGroup;

        public string CategoryId { get; private set; }

        public CanvasGroup LabelCanvasGroup => _labelCanvasGroup;

        public void Initialize(string categoryId, string displayName, Sprite icon)
        {
            CategoryId = categoryId;
            _label.text = displayName;
            _icon.sprite = icon;
        }

        public void ShowCurrent(bool current)
        {
            _currentMarker.SetActive(current);
            _label.fontStyle = FontStyles.UpperCase;
            _label.color = current ? Color.white : new Color(0.5f, 0.62f, 0.7f);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            SettingsMenu.Instance.OnCategoryConfirmed(CategoryId);
        }
    }
}
