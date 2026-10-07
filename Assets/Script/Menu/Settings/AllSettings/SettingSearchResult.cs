using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Menu.Navigation;
using YARG.Localization;

namespace YARG.Menu.Settings.AllSettings
{
    public class SettingSearchResult : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _settingText;

        [SerializeField]
        private TextMeshProUGUI _locationText;

        private string _tab;
        private string _searchName;

        public void Initialize(string localizedName, string tab, string searchName, string section)
        {
            _settingText.text = localizedName;
            var category = Localize.Key("Settings.Tab", tab);
            _locationText.text = section.Length > 0
                ? $"{category} › {Localize.Key("Settings.Header", section)}"
                : category;

            _tab = tab;
            _searchName = searchName;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm() => SettingsMenu.Instance.SelectSetting(_tab, _searchName);
    }
}
