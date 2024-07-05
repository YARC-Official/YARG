using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Localization;
using YARG.Settings.Customization;

namespace YARG.Settings.Metadata
{
    public abstract class PresetSubTab : Tab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _headerPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Header")
            .WaitForCompletion();

        public abstract CustomContent CustomContent { get; }

        protected PresetSubTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public abstract void SetPresetReference(object preset);

        protected static void SpawnHeader(Transform container, string unlocalizedText)
        {
            // Spawn in the header
            var go = Object.Instantiate(_headerPrefab, container);

            // Set header text
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                Localize.Key("Settings.Header", unlocalizedText);
        }
    }
}
