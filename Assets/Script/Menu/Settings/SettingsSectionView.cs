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

        public void ShowCurrent(bool current, bool focused = true)
        {
            _currentMarker.SetActive(current);
            _label.fontStyle = FontStyles.UpperCase;
            if (!current)
            {
                _label.color = new Color(0.5f, 0.62f, 0.7f);
            }
            else if (focused)
            {
                _label.color = Color.white;
            }
            else
            {
                _label.color = new Color(0.75f, 0.85f, 0.95f, 0.85f);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm() => SettingsMenu.Instance.EnterSettings();
    }
}
