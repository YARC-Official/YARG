using System;
using DG.Tweening;
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

        private static readonly Color ACTIVE_COLOR = Color.white;
        private static readonly Color INACTIVE_COLOR = new(0.5f, 0.62f, 0.7f);

        private Tween _fadeTween;
        private SettingsItemVisual _visual;

        public string CategoryId { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _visual = GetComponent<SettingsItemVisual>();
            _label.fontStyle = FontStyles.UpperCase;
        }

        private Action<string> _onClicked;
        private Action<string> _onConfirmed;

        public void Initialize(string categoryId, string displayName, Sprite icon,
            Action<string> onClicked = null, Action<string> onConfirmed = null)
        {
            CategoryId = categoryId;
            _label.text = displayName;
            _icon.sprite = icon;
            _onClicked = onClicked;
            _onConfirmed = onConfirmed;
        }

        public void SetCurrent(bool current)
        {
            _visual.ShowCurrent(current);
        }

        public void SetLabelAlpha(float alpha, float duration)
        {
            _fadeTween?.Kill();
            if (duration <= 0f)
            {
                _labelCanvasGroup.alpha = alpha;
                return;
            }

            _fadeTween = _labelCanvasGroup.DOFade(alpha, duration)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void ShowActive(bool current)
        {
            _currentMarker.SetActive(current);
            _label.color = current ? ACTIVE_COLOR : INACTIVE_COLOR;
            _icon.color = current ? ACTIVE_COLOR : INACTIVE_COLOR;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _onClicked?.Invoke(CategoryId);
        }

        public override void Confirm() => _onConfirmed?.Invoke(CategoryId);
    }
}
