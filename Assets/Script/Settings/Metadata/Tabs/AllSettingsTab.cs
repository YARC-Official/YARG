using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.AllSettings;

namespace YARG.Settings.Metadata
{
    public class AllSettingsTab : Tab
    {
        // Prefabs needed for this tab type
        private static GameObject _settingCategoryViewPrefab;
        private static GameObject _searchResultPopulator;

        public override bool ShowSearchBar => true;

        public AllSettingsTab() : base("AllSettings")
        {
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            if (string.IsNullOrEmpty(SettingsMenu.Instance.SearchQuery))
            {
                // If there is no search query, just show all of the categories
                foreach (var tab in SettingsManager.AllSettingsTabs)
                {
                    // Skip this tab (because we're already in it at this point)
                    if (tab is AllSettingsTab)
                    {
                        continue;
                    }

                    if (_settingCategoryViewPrefab == null) {
                        _settingCategoryViewPrefab = Addressables
                            .LoadAssetAsync<GameObject>("SettingTab/SettingCategoryView")
                            .WaitForCompletion();
                    }

                    var gameObject = Object.Instantiate(_settingCategoryViewPrefab, settingContainer);
                    var view = gameObject.GetComponent<SettingCategoryView>();
                    view.Initialize(tab);
                    navGroup.AddNavigatable(view);
                }
            }
            else
            {
                // Otherwise, show search results. Since this can't be async, we gotta do
                // something a little hacky here. Create an empty object with a search result
                // populator which will wait a little bit before spawning anything to prevent
                // lag when typing. This will spawn in the results
                if (_searchResultPopulator == null) {
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
}