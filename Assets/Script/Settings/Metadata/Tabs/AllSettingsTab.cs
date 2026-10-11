using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.AllSettings;

namespace YARG.Settings.Metadata
{
    public class AllSettingsTab : Tab
    {
        private static GameObject _searchResultPopulator;

        public AllSettingsTab() : base("AllSettings")
        {
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            if (_searchResultPopulator == null)
            {
                _searchResultPopulator = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/SearchResultPopulator")
                    .WaitForCompletion();
            }

            var gameObject = Object.Instantiate(_searchResultPopulator, settingContainer);
            var populator = gameObject.GetComponent<SearchResultPopulator>();
            populator.Initialize(SettingsMenu.Instance.SearchQuery, settingContainer, navGroup);
        }
    }
}