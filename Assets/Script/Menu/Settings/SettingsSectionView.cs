using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Localization;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings
{
    public sealed class SettingsSectionView : NavigatableBehaviour
    {
        private static readonly Color ACTIVE_COLOR = Color.white;
        private static readonly Color INACTIVE_COLOR = new(0.5f, 0.62f, 0.7f);

        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private GameObject _currentMarker;

        private SettingsItemVisual _visual;

        public string Section { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _visual = GetComponent<SettingsItemVisual>();
        }

        private Action<string> _onClicked;
        private Action _onConfirmed;

        public void Initialize(string section, Action<string> onClicked = null, Action onConfirmed = null)
        {
            Section = section;
            _onClicked = onClicked;
            _onConfirmed = onConfirmed;
            _label.text = Localize.Key("Settings.Header", section);
        }

        public void SetCurrent(bool current)
        {
            _visual.ShowCurrent(current);
        }

        public void ShowActive(bool current)
        {
            _currentMarker.SetActive(current);
            _label.color = current ? ACTIVE_COLOR : INACTIVE_COLOR;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _onClicked?.Invoke(Section);
        }

        public override void Confirm() => _onConfirmed?.Invoke();
    }
}
