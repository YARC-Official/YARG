using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace YARG.Helpers
{
    public static class LocaleHelper
    {
        public static string LocalizeString(string table, string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        }

        public static string LocalizeString(string key)
        {
            return LocalizeString("Main", key);
        }

        public static LocalizedString StringReference(string table, string key)
        {
            return new LocalizedString
            {
                TableReference = table,
                TableEntryReference = key
            };
        }

        public static LocalizedString StringReference(string key)
        {
            return StringReference("Main", key);
        }
    }
}