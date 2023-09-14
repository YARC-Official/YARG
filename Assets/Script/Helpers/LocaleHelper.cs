using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using YARG.Core;

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

        #region Enum to Localized Extensions

        public static string ToLocalizedName(this Instrument instrument)
        {
            return LocalizeString($"Instrument.{instrument}");
        }

        public static string ToLocalizedName(this Difficulty difficulty)
        {
            return LocalizeString($"Difficulty.{difficulty}");
        }

        public static string ToLocalizedName(this GameMode gameMode)
        {
            return LocalizeString($"GameMode.{gameMode}");
        }

        #endregion
    }
}