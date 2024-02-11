using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings.AllSettings
{
    public class SearchResultPopulator : MonoBehaviour
    {
        private struct SearchResult
        {
            public string Tab;
            public int Index;
            public string LocalizedName;
        }

        private const float WAIT_TIME = 0.25f;

        private const int MAX_RESULTS = 25;

        [SerializeField]
        private SettingSearchResult _resultPrefab;

        private Coroutine _coroutine;

        public void Initialize(string query, Transform container, NavigationGroup navGroup)
        {
            _coroutine = StartCoroutine(SearchCoroutine(query, container, navGroup));
        }

        private IEnumerator SearchCoroutine(string query, Transform container, NavigationGroup navGroup)
        {
            yield return new WaitForSeconds(WAIT_TIME);

            query = query.ToLowerInvariant();

            var results = new List<SearchResult>();
            foreach (var tab in SettingsManager.AllSettingsTabs)
            {
                if (tab is not MetadataTab metadataTab)
                {
                    continue;
                }

                for (int i = 0; i < metadataTab.Settings.Count; i++)
                {
                    var metadata = metadataTab.Settings[i];
                    var unlocalizedSearch = metadata.UnlocalizedSearchNames;
                    if (unlocalizedSearch is null)
                    {
                        continue;
                    }

                    foreach (var unlocalized in unlocalizedSearch)
                    {
                        var localized = LocaleHelper.LocalizeString("Settings", unlocalized);
                        if (localized.ToLowerInvariant().Contains(query))
                        {
                            results.Add(new SearchResult
                            {
                                Tab = tab.Name,
                                Index = i,
                                LocalizedName = localized
                            });
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
                resultObject.Initialize(result.LocalizedName);
                navGroup.AddNavigatable(resultObject);
            }
        }

        public void OnDestroy()
        {
            StopCoroutine(_coroutine);
        }
    }
}