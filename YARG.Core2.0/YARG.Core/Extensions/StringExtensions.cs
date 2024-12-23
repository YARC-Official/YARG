using System;

namespace YARG.Core.Extensions
{
    public static class StringExtensions
    {
        private interface ICharacterTrimmer
        {
            bool ShouldBeTrimmed(char c);
        }

        private readonly struct AsciiWhitespaceTrimmer : ICharacterTrimmer
        {
            public readonly bool ShouldBeTrimmed(char c)
                => c.IsAsciiWhitespace();
        }

        private readonly struct Latin1WhitespaceTrimmer : ICharacterTrimmer
        {
            public readonly bool ShouldBeTrimmed(char c)
                => c.IsLatin1Whitespace();
        }

        /// <summary>
        /// Removes all leading and trailing ASCII whitespace characters from the string.
        /// </summary>
        public static string TrimAscii(this string str)
            => str.AsSpan().TrimAscii().ToString();

        /// <summary>
        /// Removes all leading and trailing ASCII whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimAscii(this ReadOnlySpan<char> buffer)
            => buffer.Trim(new AsciiWhitespaceTrimmer());

        /// <summary>
        /// Removes all leading ASCII whitespace characters from the string.
        /// </summary>
        public static string TrimStartAscii(this string str)
            => str.AsSpan().TrimStartAscii().ToString();

        /// <summary>
        /// Removes all leading ASCII whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimStartAscii(this ReadOnlySpan<char> buffer)
            => buffer.TrimStart(new AsciiWhitespaceTrimmer());

        /// <summary>
        /// Removes all trailing ASCII whitespace characters from the string.
        /// </summary>
        public static string TrimEndAscii(this string str)
            => str.AsSpan().TrimEndAscii().ToString();

        /// <summary>
        /// Removes all trailing ASCII whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimEndAscii(this ReadOnlySpan<char> buffer)
            => buffer.TrimEnd(new AsciiWhitespaceTrimmer());

        /// <summary>
        /// Removes all leading and trailing Latin-1 whitespace characters from the string.
        /// </summary>
        public static string TrimLatin1(this string str)
            => str.AsSpan().TrimLatin1().ToString();

        /// <summary>
        /// Removes all leading and trailing Latin-1 whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimLatin1(this ReadOnlySpan<char> buffer)
            => buffer.Trim(new Latin1WhitespaceTrimmer());

        /// <summary>
        /// Removes all leading Latin-1 whitespace characters from the string.
        /// </summary>
        public static string TrimStartLatin1(this string str)
            => str.AsSpan().TrimStartLatin1().ToString();

        /// <summary>
        /// Removes all leading Latin-1 whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimStartLatin1(this ReadOnlySpan<char> buffer)
            => buffer.TrimStart(new Latin1WhitespaceTrimmer());

        /// <summary>
        /// Removes all trailing Latin-1 whitespace characters from the string.
        /// </summary>
        public static string TrimEndLatin1(this string str)
            => str.AsSpan().TrimEndLatin1().ToString();

        /// <summary>
        /// Removes all trailing Latin-1 whitespace characters from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimEndLatin1(this ReadOnlySpan<char> buffer)
            => buffer.TrimEnd(new Latin1WhitespaceTrimmer());

        /// <summary>
        /// Removes all leading and trailing characters from the read-only character span, based on the given trimmer.
        /// </summary>
        private static ReadOnlySpan<char> Trim<TTrimmer>(this ReadOnlySpan<char> buffer, TTrimmer trimmer)
            where TTrimmer : ICharacterTrimmer
        {
            int start = 0;
            int end = buffer.Length;

            while (start < buffer.Length && trimmer.ShouldBeTrimmed(buffer[start]))
                start++;

            while (end > start && trimmer.ShouldBeTrimmed(buffer[end - 1]))
                end--;

            return buffer[start..end];
        }

        /// <summary>
        /// Removes all leading characters from the read-only character span, based on the given trimmer.
        /// </summary>
        private static ReadOnlySpan<char> TrimStart<TTrimmer>(this ReadOnlySpan<char> buffer, TTrimmer trimmer)
            where TTrimmer : ICharacterTrimmer
        {
            int start = 0;

            while (start < buffer.Length && trimmer.ShouldBeTrimmed(buffer[start]))
                start++;

            return buffer[start..];
        }

        /// <summary>
        /// Removes all trailing characters from the read-only character span, based on the given trimmer.
        /// </summary>
        private static ReadOnlySpan<char> TrimEnd<TTrimmer>(this ReadOnlySpan<char> buffer, TTrimmer trimmer)
            where TTrimmer : ICharacterTrimmer
        {
            int end = buffer.Length;

            while (end > 0 && trimmer.ShouldBeTrimmed(buffer[end - 1]))
                end--;

            return buffer[..end];
        }

        /// <summary>
        /// Removes up to a single leading and a single trailing occurrence of a specified character from the read-only
        /// character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[0] == trimChar)
                buffer = buffer[1..];
            if (!buffer.IsEmpty && buffer[^1] == trimChar)
                buffer = buffer[..^1];

            return buffer;
        }

        /// <summary>
        /// Removes up to a single leading occurrence of a specified character from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimStartOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[0] == trimChar)
                buffer = buffer[1..];

            return buffer;
        }

        /// <summary>
        /// Removes up to a single trailing occurrence of a specified character from the read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimEndOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[^1] == trimChar)
                buffer = buffer[..^1];

            return buffer;
        }
    }
}