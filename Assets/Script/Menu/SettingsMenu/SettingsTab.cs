using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Settings;

namespace YARG.Menu.Settings
{
    public class SettingsTab : MonoBehaviour
    {
        [SerializeField]
        private Image icon;

        [SerializeField]
        private Button button;

        [SerializeField]
        private LocalizeStringEvent text;

        private string tabName;

        public void SetTab(string tabName, string iconName)
        {
            this.tabName = tabName;

            // Set icon
            icon.sprite = Addressables.LoadAssetAsync<Sprite>($"SettingIcons[{iconName}]").WaitForCompletion();

            // Set text
            text.StringReference = LocaleHelper.StringReference("Settings", $"Tab.{tabName}");
        }

        private void Update()
        {
            if (tabName == null)
            {
                return;
            }

            button.interactable = SettingsMenu.Instance.CurrentTab != tabName;
        }

        public void OnTabClick()
        {
            SettingsMenu.Instance.CurrentTab = tabName;
        }
    }
}