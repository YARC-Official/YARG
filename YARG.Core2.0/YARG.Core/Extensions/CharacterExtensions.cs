using System.Globalization;
using System.Runtime.CompilerServices;

namespace YARG.Core.Extensions
{
    // A majority of this is modified from mscorlib
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Char.cs
    public static class CharacterExtensions
    {
        /// <summary>
        /// Indicates whether a character is within the specified inclusive range.
        /// </summary>
        /// <remarks>
        /// The method does not validate that <paramref name="maxInclusive"/> is greater than or equal
        /// to <paramref name="minInclusive"/>.  If <paramref name="maxInclusive"/> is less than
        /// <paramref name="minInclusive"/>, the behavior is undefined.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetween(this char c, char minInclusive, char maxInclusive)
            => (uint) (c - minInclusive) <= (uint) (maxInclusive - minInclusive);

        /// <summary>
        /// Indicates whether a Unicode category is within the specified inclusive range.
        /// </summary>
        /// <remarks>
        /// The method does not validate that <paramref name="maxInclusive"/> is greater than or equal
        /// to <paramref name="minInclusive"/>.  If <paramref name="maxInclusive"/> is less than
        /// <paramref name="minInclusive"/>, the behavior is undefined.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetween(this UnicodeCategory c, UnicodeCategory minInclusive, UnicodeCategory maxInclusive)
            => (uint) (c - minInclusive) <= (uint) (maxInclusive - minInclusive);

        #region ASCII
        public const byte ASCII_LOWERCASE_FLAG = 0x20;
        public const byte ASCII_MAX_VALUE = 0x7F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetAsciiCharacterInfo(this char c)
            => c.IsAscii() ? Latin1CharInfo[c] : LATIN1_INFO_DEFAULT;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnicodeCategory GetAsciiUnicodeCategory(this char c)
            => (UnicodeCategory) (GetAsciiCharacterInfo(c) & LATIN1_CATEGORY_MASK);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAscii(this char c)
            => (uint) c <= ASCII_MAX_VALUE;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetter(this char c)
            => IsBetween((char) (c | ASCII_LOWERCASE_FLAG), 'a', 'z');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetterUpper(this char c)
            => IsBetween(c, 'A', 'Z');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetterLower(this char c)
            => IsBetween(c, 'a', 'z');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiDigit(this char c)
            => c.IsBetween('0', '9');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiWhitespace(this char c)
            => (uint) c <= 32;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ToAsciiUpper(this char c)
            => (char) (c & ~ASCII_LOWERCASE_FLAG);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ToAsciiLower(this char c)
            => (char) (c | ASCII_LOWERCASE_FLAG);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAsciiToNumber(this char c, out int value)
        {
            uint converted = (uint) c - '0';
            value = (int) converted;
            return converted <= 9;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAsciiToNumber(this char c, out uint value)
        {
            value = (uint) c - '0';
            return value <= 9;
        }
        #endregion

        #region Latin1
        // Private implementation details for the table below
        private const byte LATIN1_WHITESPACE_FLAG = 0x80;
        private const byte LATIN1_UPPERCASE_FLAG = 0x40;
        private const byte LATIN1_LOWERCASE_FLAG = 0x20;
        private const byte LATIN1_CATEGORY_MASK = 0x1F;
        private const byte LATIN1_INFO_DEFAULT = (byte) UnicodeCategory.OtherNotAssigned;

        // Contains information about the C0, Basic Latin, C1, and Latin-1 Supplement ranges [ U+0000..U+00FF ]:
        // - 0x80 means 'is whitespace'
        // - 0x40 means 'is uppercase letter'
        // - 0x20 means 'is lowercase letter'
        // - bottom 5 bits are the UnicodeCategory of the character
        private static readonly byte[] Latin1CharInfo = new byte[]
        {
            // 0     1     2     3     4     5     6     7     8     9     A     B     C     D     E     F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x8E, 0x8E, 0x8E, 0x8E, 0x8E, 0x0E, 0x0E, // U+0000..U+000F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, // U+0010..U+001F
            0x8B, 0x18, 0x18, 0x18, 0x1A, 0x18, 0x18, 0x18, 0x14, 0x15, 0x18, 0x19, 0x18, 0x13, 0x18, 0x18, // U+0020..U+002F
            0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x18, 0x18, 0x19, 0x19, 0x19, 0x18, // U+0030..U+003F
            0x18, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, // U+0040..U+004F
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x14, 0x18, 0x15, 0x1B, 0x12, // U+0050..U+005F
            0x1B, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, // U+0060..U+006F
            0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x14, 0x19, 0x15, 0x19, 0x0E, // U+0070..U+007F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x8E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, // U+0080..U+008F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, // U+0090..U+009F
            0x8B, 0x18, 0x1A, 0x1A, 0x1A, 0x1A, 0x1C, 0x18, 0x1B, 0x1C, 0x04, 0x16, 0x19, 0x0F, 0x1C, 0x1B, // U+00A0..U+00AF
            0x1C, 0x19, 0x0A, 0x0A, 0x1B, 0x21, 0x18, 0x18, 0x1B, 0x0A, 0x04, 0x17, 0x0A, 0x0A, 0x0A, 0x18, // U+00B0..U+00BF
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, // U+00C0..U+00CF
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x19, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x21, // U+00D0..U+00DF
            0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, // U+00E0..U+00EF
            0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x19, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, 0x21, // U+00F0..U+00FF
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetLatin1CharacterInfo(this char c)
            => c.IsLatin1() ? Latin1CharInfo[c] : LATIN1_INFO_DEFAULT;

        /// <summary>
        /// Gets the Unicode category for the given Latin-1 character.
        /// </summary>
        /// <returns>
        /// If the character is Latin-1, its corresponding category; otherwise <see cref="UnicodeCategory.OtherNotAssigned"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnicodeCategory GetLatin1UnicodeCategory(this char c)
            => (UnicodeCategory) (c.GetLatin1CharacterInfo() & LATIN1_CATEGORY_MASK);

        /// <summary>
        /// Determines whether or not the given character is Latin-1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1(this char c)
            => (uint) c < Latin1CharInfo.Length;

        /// <summary>
        /// Determines whether or not the given character is a Latin-1 letter character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1Letter(this char c)
            => c.GetLatin1UnicodeCategory().IsBetween(UnicodeCategory.UppercaseLetter, UnicodeCategory.OtherLetter);

        /// <summary>
        /// Determines whether or not the given character is a Latin-1 uppercase letter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1LetterUpper(this char c)
            => (c.GetLatin1CharacterInfo() & LATIN1_UPPERCASE_FLAG) != 0;

        /// <summary>
        /// Determines whether or not the given character is a Latin-1 lowercase letter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1LetterLower(this char c)
            => (c.GetLatin1CharacterInfo() & LATIN1_LOWERCASE_FLAG) != 0;

        /// <summary>
        /// Determines whether or not the given character is a Latin-1 number character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1Number(this char c)
            => c.GetLatin1UnicodeCategory().IsBetween(UnicodeCategory.DecimalDigitNumber, UnicodeCategory.OtherNumber);

        /// <summary>
        /// Determines whether or not the given character is a Latin-1 digit character (0-9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1Digit(this char c)
            => c.GetLatin1UnicodeCategory() == UnicodeCategory.DecimalDigitNumber;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1Punctuation(this char c)
            => c.GetLatin1UnicodeCategory().IsBetween(UnicodeCategory.ConnectorPunctuation, UnicodeCategory.OtherPunctuation);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1Whitespace(this char c)
            => c.GetLatin1UnicodeCategory().IsBetween(UnicodeCategory.SpaceSeparator, UnicodeCategory.Surrogate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin1WhitespaceOnly(this char c)
            => (c.GetLatin1CharacterInfo() & LATIN1_WHITESPACE_FLAG) != 0;
        #endregion
    }
}