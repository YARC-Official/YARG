using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;

namespace YARG.Settings.Metadata
{
    public class SongManagerTab : MetadataTab
    {
        // Prefabs needed for this tab type
        private static GameObject _songManagerHeader;
        private static GameObject _songManagerDirectory;

        public SongManagerTab(string name, string icon = "Generic") : base(name, icon)
        {
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            if (_songManagerHeader == null)
            {
                 _songManagerHeader = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/SongManagerHeader")
                    .WaitForCompletion();
            }
            if (_songManagerDirectory == null)
            {
                 _songManagerDirectory = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/SongManagerDirectory")
                    .WaitForCompletion();
            }

            // Spawn in the special header
            Object.Instantiate(_songManagerHeader, settingContainer);

            // Create all of the directories
            for (int i = 0; i < SettingsManager.Settings.SongFolders.Count; i++)
            {
                var go = Object.Instantiate(_songManagerDirectory, settingContainer);
                go.GetComponent<SettingsDirectory>().SetIndex(i);
            }

            // Build the rest of the metadata
            base.BuildSettingTab(settingContainer, navGroup);
        }
    }
}