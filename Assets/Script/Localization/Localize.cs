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
    public static class Localize
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

        public static string Key<T1, T2>(T1 concat1, T2 concat2)
        {
            return Key(ZString.Concat(concat1, '.', concat2));
        }

        public static string Key<T1, T2, T3>(T1 concat1, T2 concat2, T3 concat3)
        {
            return Key(ZString.Concat(concat1, '.', concat2, '.', concat3));
        }

        public static string Key<T1, T2, T3, T4>(T1 concat1, T2 concat2, T3 concat3, T4 concat4)
        {
            return Key(ZString.Concat(concat1, '.', concat2, '.', concat3, '.', concat4));
        }

        #endregion

        #region Key with Formatting

        public static string KeyFormat<T1>(string key, T1 arg1)
        {
            return ZString.Format(Key(key), arg1);
        }

        public static string KeyFormat<T1, T2>(string key, T1 arg1, T2 arg2)
        {
            return ZString.Format(Key(key), arg1, arg2);
        }

        public static string KeyFormat<T1, T2, T3>(string key, T1 arg1, T2 arg2, T3 arg3)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3);
        }

        public static string KeyFormat<T1, T2, T3, T4>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5, T6>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5,
            T6 arg6)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5, arg6);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5, T6, T7>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5, T6 arg6, T7 arg7)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string key, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static string KeyFormat<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string key, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            return ZString.Format(Key(key), arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
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