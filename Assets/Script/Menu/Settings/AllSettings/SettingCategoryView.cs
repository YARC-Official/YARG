using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings.AllSettings
{
    public class SettingCategoryView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _categoryTitle;
        [SerializeField]
        private Image _icon;

        private string _tabName;

        public void Initialize(Tab tab)
        {
            _tabName = tab.Name;

            _categoryTitle.text = Localize.Key("Settings.Tab", tab.Name);

            var sprite = Addressables
                .LoadAssetAsync<Sprite>($"TabIcons[{tab.Icon}]")
                .WaitForCompletion();
            _icon.sprite = sprite;
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