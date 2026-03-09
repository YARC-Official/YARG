using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings.AllSettings
{
    public class SettingSearchResult : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _settingText;

        private string _tab;
        private int _index;
        private bool _isAdvanced;

        public void Initialize(string localizedName, string tab, int index, bool isAdvanced)
        {
            _settingText.text = localizedName;

            _tab = tab;
            _index = index;
            _isAdvanced = isAdvanced;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            if (_isAdvanced)
            {
                SettingsMenu.Instance.EnableAdvanced(true);
            }

            SettingsMenu.Instance.SelectTabByName(_tab);
            SettingsMenu.Instance.SelectSettingByIndex(_index);

            if (_isAdvanced)
            {
                SettingsMenu.Instance.RefreshNavigationScheme();
            }
        }
    }
}
