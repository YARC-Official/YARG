using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Localization;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings
{
    public sealed class SettingsSectionView : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private GameObject _currentMarker;

        public string Section { get; private set; }

        public void Initialize(string section)
        {
            Section = section;
            _label.text = Localize.Key("Settings.Header", section);
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

        public override void Confirm() => SettingsMenu.Instance.EnterSettings();
    }
}
