using System.Globalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using YARG.Core;
using YARG.Core.Game;
using YARG.Song;

namespace YARG.Helpers
{
    public static class LocaleHelper
    {
        public static readonly LocalizedString EmptyString = new();

        public static readonly NumberFormatInfo PercentFormat = new()
        {
            // Display as "100%" instead of "100 %"
            PercentPositivePattern = 1,
            PercentNegativePattern = 1,
        };

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

        public static string ToLocalizedName(this Modifier modifier)
        {
            return LocalizeString($"Modifier.{modifier}");
        }

        public static string ToLocalizedName(this SortAttribute sortAttribute)
        {
            return LocalizeString($"SortAttribute.{sortAttribute}");
        }

        #endregion
    }
}