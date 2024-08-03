using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Localization
{
    public static class LocalizationManager
    {
        private const string DEFAULT_CULTURE = "en-US";

        public static string CultureCode { get; private set; }

        private static readonly Dictionary<string, string> _localizationMap = new();

        /// <summary>
        /// Initializes the localization manager.
        /// <b>This does not load the language!</b> Use <see cref="LoadLanguage"/> for that.
        /// </summary>
        public static void Initialize(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode))
            {
                CultureCode = DEFAULT_CULTURE;
            }
            else
            {
                CultureCode = cultureCode;
            }

            YargLogger.LogFormatInfo("Localization initialized with language `{0}`", CultureCode);
        }

        public static async UniTask LoadLanguage(LoadingContext loadingContext)
        {
            loadingContext.SetLoadingText("Loading language...");
            await UniTask.RunOnThreadPool(() =>
            {
                // Attempt to load the selected language
                if (!TryParseAndLoadLanguage(CultureCode))
                {
                    if (CultureCode != DEFAULT_CULTURE)
                    {
                        // If that fails for whatever reason, load the default one instead
                        YargLogger.LogError("Failed to parse and load language! Falling back to default.");

                        CultureCode = DEFAULT_CULTURE;
                        if (!TryParseAndLoadLanguage(CultureCode))
                        {
                            YargLogger.LogError("Failed to parse and load default language!");
                        }
                    }
                    else
                    {
                        YargLogger.LogError("Failed to parse and load the default language! (no fallback)");
                    }
                }
            });
        }

        private static bool TryParseAndLoadLanguage(string cultureCode)
        {
            YargLogger.LogFormatInfo("Loading language `{0}`...", cultureCode);

            try
            {
                _localizationMap.Clear();

                ParseAndLoadLanguage(cultureCode);

                // Also combine the keys of the default culture. The default culture is guaranteed to be
                // the most up to date as that is the one attached to the repo. The other languages are
                // fetched periodically from Crowdin which means there may be some desync.
                if (cultureCode != DEFAULT_CULTURE)
                {
                    ParseAndLoadLanguage(DEFAULT_CULTURE);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                return false;
            }

            return true;
        }

        private static void ParseAndLoadLanguage(string cultureCode)
        {
            // Get the path of the localization file
            var file = Path.Combine(PathHelper.StreamingAssetsPath, "lang", $"{cultureCode}.json");
            if (!File.Exists(file))
            {
                throw new Exception($"The language file for the specified culture ({cultureCode}) does not exist!");
            }

            // Read, parse, and scan for localization keys
            var json = File.ReadAllText(file);
            var obj = JObject.Parse(json);
            ParseObjectRecursive(null, obj);
        }

        private static void ParseObjectRecursive(string parentKey, JObject obj)
        {
            foreach ((string key, var token) in obj)
            {
                if (token is null)
                {
                    YargLogger.LogWarning("Found `null` token while parsing language. Skipping.");
                    continue;
                }

                // Construct the full key
                string fullKey = key;
                if (!string.IsNullOrEmpty(parentKey))
                {
                    fullKey = ZString.Concat(parentKey, '.', key);
                }

                switch (token.Type)
                {
                    // If an object is found, recursively scan for more keys
                    case JTokenType.Object:
                        ParseObjectRecursive(fullKey, token.ToObject<JObject>());
                        break;
                    // If a string is found, that's the end! Add it to the localization map.
                    case JTokenType.String:
                        _localizationMap.TryAdd(fullKey, token.ToString());
                        break;
                    // Otherwise... something went wrong.
                    default:
                        YargLogger.LogFormatWarning("Found `{0}` token while parsing language. Skipping", token.Type);
                        break;
                }
            }
        }

        public static bool TryGetLocalizedKey(string key, out string value)
        {
            return _localizationMap.TryGetValue(key, out value);
        }
    }
}