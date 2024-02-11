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

        public void Initialize(string localizedName, string tab, int index)
        {
            _settingText.text = localizedName;

            _tab = tab;
            _index = index;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            SettingsMenu.Instance.SelectTabByName(_tab);
            SettingsMenu.Instance.SelectSettingByIndex(_index);
        }
    }
}