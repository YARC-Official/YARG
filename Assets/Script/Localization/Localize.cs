using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cysharp.Text;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Song;

namespace YARG.Localization
{
    public static partial class Localize
    {
        #region Key

        public static string Key(string key)
        {
            if (LocalizationManager.TryGetLocalizedKey(key, out var value))
            {
                return value;
            }

            YargLogger.LogFormatWarning("No translation found for key `{0}`.", key);
            return key;
        }

        #endregion

        #region Lists

        public static string List(IEnumerable<string> elements)
        {
            return List((ReadOnlySpan<string>) elements.ToArray());
        }

        public static string List(ReadOnlySpan<string> elements)
        {
            if (elements.Length == 0)
            {
                return string.Empty;
            }

            if (elements.Length == 1)
            {
                return elements[0];
            }

            using var sb = ZString.CreateStringBuilder();

            // Join all but the last elements together
            sb.AppendJoin(Key("Language.ListDelimiter.Default"), elements[..^1]);

            // Append last element (special case)
            sb.Append(Key("Language.ListDelimiter.Last"));
            sb.Append(elements[^1]);

            return sb.ToString();
        }

        #endregion

        #region Other

        private static readonly NumberFormatInfo _percentFormat = new()
        {
            // Display as "100%" instead of "100 %"
            PercentPositivePattern = 1,
            PercentNegativePattern = 1,
        };

        public static string Percent(float value, int decimalPlaces = 0)
        {
            return value.ToString(ZString.Concat("P", decimalPlaces), _percentFormat);
        }

        #endregion

        #region Enum to Localized Extensions

        public static string ToLocalizedName(this Difficulty difficulty)
        {
            return Key("Enum.Difficulty", difficulty);
        }

        public static string ToLocalizedName(this GameMode gameMode)
        {
            return Key("Enum.GameMode", gameMode);
        }

        public static string ToLocalizedName(this Instrument instrument)
        {
            return Key("Enum.Instrument", instrument);
        }

        public static string ToLocalizedName(this Modifier modifier)
        {
            return Key("Enum.Modifier", modifier);
        }

        public static string ToLocalizedName(this SortAttribute sortAttribute)
        {
            return Key("Enum.SortAttribute", sortAttribute);
        }

        #endregion
    }
}