using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG.Menu.Settings.AllSettings
{
    public class SettingCategoryView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _categoryTitle;

        private string _tabName;

        public void Initialize(string tabName)
        {
            _tabName = tabName;

            _categoryTitle.text = LocaleHelper.LocalizeString("Settings", $"Tab.{tabName}");
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            SettingsMenu.Instance.SelectTabByName(_tabName);
        }
    }
}