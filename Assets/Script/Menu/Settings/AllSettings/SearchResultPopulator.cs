using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings.AllSettings
{
    public class SearchResultPopulator : MonoBehaviour
    {
        private readonly struct SearchResult
        {
            public SearchResult(string tab, string searchName, string localizedName, string section)
            {
                Tab = tab;
                SearchName = searchName;
                LocalizedName = localizedName;
                Section = section;
            }

            public readonly string Tab;
            public readonly string SearchName;
            public readonly string LocalizedName;
            public readonly string Section;
        }

        private const float WAIT_TIME = 0.25f;

        private const int MAX_RESULTS = 25;

        [SerializeField]
        private SettingSearchResult _resultPrefab;

        public void Initialize(string query, Transform container, NavigationGroup navGroup)
        {
            StartCoroutine(SearchCoroutine(query, container, navGroup));
        }

        private IEnumerator SearchCoroutine(string query, Transform container, NavigationGroup navGroup)
        {
            yield return new WaitForSeconds(WAIT_TIME);

            var normalizedQuery = query.ToLowerInvariant();

            var results = new List<SearchResult>();
            foreach (var tab in SettingsManager.AllSettingsTabs)
            {
                if (tab is not MetadataTab metadataTab)
                {
                    continue;
                }

                var section = string.Empty;
                foreach (var metadata in metadataTab.Settings)
                {
                    if (!metadata.IsVisible)
                    {
                        continue;
                    }
                    if (metadata is HeaderMetadata header)
                    {
                        section = header.HeaderName;
                    }
                    var unlocalizedSearch = metadata.UnlocalizedSearchNames;
                    if (unlocalizedSearch is null)
                    {
                        continue;
                    }

                    foreach (var unlocalized in unlocalizedSearch)
                    {
                        var localized = Localize.Key("Settings", unlocalized);
                        if (localized.ToLowerInvariant().Contains(normalizedQuery))
                        {
                            results.Add(new SearchResult(tab: tab.Name, searchName: unlocalized,
                                localizedName: localized, section: section));
                            break;
                        }
                    }

                }

                if (results.Count >= MAX_RESULTS)
                {
                    break;
                }
            }

            // Allow the coroutine to stop before everything gets spawned in
            yield return null;

            foreach (var result in results)
            {
                var resultObject = Instantiate(_resultPrefab, container);
                resultObject.Initialize(localizedName: result.LocalizedName, tab: result.Tab,
                    searchName: result.SearchName, section: result.Section);
                navGroup.AddNavigatable(resultObject);
            }
        }
    }
}
