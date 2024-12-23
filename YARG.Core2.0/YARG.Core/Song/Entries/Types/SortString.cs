using System;
using System.Globalization;
using System.Text;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    public enum CharacterGroup
    {
        Empty,
        AsciiSymbol,
        AsciiNumber,
        AsciiLetter,
        NonAscii
    }

    public readonly struct SortString : IComparable<SortString>, IEquatable<SortString>
    {
        // Order of these static variables matters
        private static readonly (string, string)[] SearchLeniency =
        {
            ("Æ", "AE") // Tool - Ænema
        };

        public static readonly SortString Empty = new(string.Empty, string.Empty, string.Empty);

        public static SortString Convert(string str)
        {
            string searchStr = RemoveUnwantedWhitespace(RemoveDiacritics(RichTextUtils.StripRichTextTags(str)));
            string sortStr = RemoveArticle(searchStr);
            return new SortString(str, searchStr, sortStr);
        }

        public readonly string Str;
        public readonly string SearchStr;
        public readonly string SortStr;
        public readonly int HashCode;
        public readonly CharacterGroup Group;

        public int Length => Str.Length;

        private SortString(string str, string searchStr, string sortStr)
        {
            Str = str;
            SearchStr = searchStr;
            SortStr = sortStr;
            HashCode = sortStr.GetHashCode();
            Group = sortStr.Length > 0 ? GetCharacterGrouping(SortStr[0]) : CharacterGroup.Empty;
        }

        public int CompareTo(SortString other)
        {
            if (Group != other.Group)
            {
                return Group - other.Group;
            }
            return SortStr.CompareTo(other.SortStr);
        }

        public override int GetHashCode()
        {
            return HashCode;
        }

        public bool Equals(SortString other)
        {
            return SortStr.Equals(other.SortStr);
        }

        public override string ToString()
        {
            return Str;
        }

        public static SortString Combine(in SortString a, in SortString b)
        {
            string str = a.Str + " - " + b.Str;
            string sortStr = a.SortStr + b.SortStr;
            return new SortString(str, string.Empty, sortStr);
        }

        private static CharacterGroup GetCharacterGrouping(char character)
        {
            if ('a' <= character && character <= 'z')
            {
                return CharacterGroup.AsciiLetter;
            }
            if ('0' <= character && character <= '9')
            {
                return CharacterGroup.AsciiNumber;
            }
            return character > 127 ? CharacterGroup.NonAscii : CharacterGroup.AsciiSymbol;
        }

        public static implicit operator SortString(string str) => Convert(str);
        public static implicit operator string(SortString str) => str.Str;

        private static readonly string[] Articles =
        {
            "the ", // The beatles, The day that never comes
            "el ",  // El final, El sol no regresa
            "la ",  // La quinta estacion, La bamba, La muralla verde
            "le ",  // Le temps de la rentrée
            "les ", // Les Rita Mitsouko, Les Wampas
            "los ", // Los fabulosos cadillacs, Los enanitos verdes,
        };

        public static string RemoveArticle(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var article in Articles)
                {
                    if (StartsWith(name, article))
                    {
                        return name[article.Length..];
                    }
                }
            }
            return name;
        }

        // Why use a custom function vs. .NET's built-in one? Because hot paths baby! YIPPEEEEEE!
        // Also, the use case is very controlled, so this won't hurt
        private static bool StartsWith(string str, string query)
        {
            if (str.Length < query.Length)
            {
                return false;
            }

            for (var i = 0; i < query.Length; i++)
            {
                if (char.ToLowerInvariant(str[i]) != query[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static string RemoveDiacritics(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            foreach (var c in SearchLeniency)
            {
                text = text.Replace(c.Item1, c.Item2);
            }

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            unsafe
            {
                var buffer = stackalloc char[normalizedString.Length];
                int length = 0;
                foreach (char c in normalizedString)
                {
                    switch (CharUnicodeInfo.GetUnicodeCategory(c))
                    {
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.Format:
                        case UnicodeCategory.SpacingCombiningMark:
                            break;
                        default:
                            buffer[length++] = c;
                            break;
                    }
                }

                if (length < normalizedString.Length)
                {
                    normalizedString = new string(buffer, 0, length);
                }
                return normalizedString.ToLowerInvariant().Normalize(NormalizationForm.FormC);
            }
        }

        public static unsafe string RemoveUnwantedWhitespace(string arg)
        {
            var buffer = stackalloc char[arg.Length];
            int length = 0;
            int index = 0;
            while (index < arg.Length)
            {
                char curr = arg[index++];
                if (curr > 32 || (length > 0 && buffer[length - 1] > 32))
                {
                    if (curr > 32)
                    {
                        buffer[length++] = curr;
                    }
                    else
                    {
                        while (index < arg.Length && arg[index] <= 32)
                        {
                            index++;
                        }

                        if (index == arg.Length)
                        {
                            break;
                        }

                        buffer[length++] = ' ';
                    }
                }
            }
            return length == arg.Length ? arg : new string(buffer, 0, length);
        }
    }
}
