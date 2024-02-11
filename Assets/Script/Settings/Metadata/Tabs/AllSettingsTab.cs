using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.AllSettings;

namespace YARG.Settings.Metadata
{
    public class AllSettingsTab : Tab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _settingCategoryViewPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/SettingCategoryView")
            .WaitForCompletion();

        public override bool ShowSearchBar => true;

        public AllSettingsTab() : base("AllSettings")
        {
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            foreach (var tab in SettingsManager.AllSettingsTabs)
            {
                // Skip this tab (because we're already in it at this point)
                // and also the presets tab (since it's special)
                if (tab is AllSettingsTab or PresetsTab)
                {
                    continue;
                }

                var gameObject = Object.Instantiate(_settingCategoryViewPrefab, settingContainer);
                var view = gameObject.GetComponent<SettingCategoryView>();
                view.Initialize(tab.Name);
                navGroup.AddNavigatable(view);
            }
        }
    }
}