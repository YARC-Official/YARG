using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings.ToastHistory;

namespace YARG.Settings.Metadata
{
    public class ToastHistoryTab : Tab
    {
        // Prefabs needed for this tab type
        private static GameObject _entryPrefab;
        private static GameObject _headerPrefab;

        public ToastHistoryTab(string name, string icon = "History") : base(name, icon)
        {
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            if (_entryPrefab == null)
            {
                _entryPrefab = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/ToastHistoryEntry")
                    .WaitForCompletion();
            }

            if (_headerPrefab == null)
            {
                _headerPrefab = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/Header")
                    .WaitForCompletion();
            }

            // Header
            var header = SpawnHeader(settingContainer, "ToastHistory");

            // Newest toasts first
            var entries = ToastManager.ToastHistory.Reverse().ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var toast = entries[i];
                var entry = Object.Instantiate(_entryPrefab, settingContainer);
                var visual = entry.GetComponent<ToastHistoryEntryVisual>();

                var (color, icon) = ToastManager.Instance.GetStyle(toast.Type);
                visual.Initialize(toast.Text(), color, icon);
                visual.AssignIndex(i);
                navGroup.AddNavigatable(visual.gameObject);
            }
        }

        protected static GameObject SpawnHeader(Transform container, string unlocalizedText)
        {
            var go = Object.Instantiate(_headerPrefab, container);
            go.GetComponentInChildren<TextMeshProUGUI>().text = Localize.Key("Settings.Header", unlocalizedText);
            return go;
        }
    }
}
