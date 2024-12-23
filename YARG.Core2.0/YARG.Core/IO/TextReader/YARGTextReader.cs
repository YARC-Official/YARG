using System;
using System.ComponentModel;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class TextConstants<TChar>
            where TChar : unmanaged
    {
        public static readonly TChar NEWLINE;
        public static readonly TChar OPEN_BRACKET;
        public static readonly TChar CLOSE_BRACE;

        static unsafe TextConstants()
        {
            int newline = '\n';
            int openBracket = '[';
            int closeBrace = '}';
            NEWLINE = *(TChar*) &newline;
            OPEN_BRACKET = *(TChar*) &openBracket;
            CLOSE_BRACE = *(TChar*) &closeBrace;
        }
    }

    public static unsafe class YARGTextReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        public static readonly Encoding UTF8Strict = new UTF8Encoding(false, true);
        public static readonly Encoding UTF32BE = new UTF32Encoding(true, false);

        public static bool IsUTF8(in FixedArray<byte> data, out YARGTextContainer<byte> container)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                container = default;
                return false;
            }

            container = new YARGTextContainer<byte>(in data, UTF8Strict);
            if (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                container.Position += 3;
            }
            SkipPureWhitespace(ref container);
            return true;
        }

        public static FixedArray<char> ConvertToUTF16(in FixedArray<byte> data, out YARGTextContainer<char> container)
        {
            if (data[2] == 0)
            {
                container = default;
                return FixedArray<char>.Null;
            }

            const int UTF16BOM_OFFSET = 2;
            FixedArray<char> buffer;
            long length = (data.Length - UTF16BOM_OFFSET) / sizeof(char);
            if ((data[0] == 0xFF) != BitConverter.IsLittleEndian)
            {
                // We have to swap the endian of the data so string conversion works properly
                // but we can't just use the original buffer as we create a hash off it.
                buffer = FixedArray<char>.Alloc(length);
                for (int i = 0, j = UTF16BOM_OFFSET; i < buffer.Length; ++i, j += sizeof(char))
                {
                    buffer.Ptr[i] = (char) (data.Ptr[j] << 8 | data.Ptr[j + 1]);
                }
            }
            else
            {
                buffer = FixedArray<char>.Cast(in data, UTF16BOM_OFFSET, length);
            }
            container = new YARGTextContainer<char>(in buffer, data[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);
            SkipPureWhitespace(ref container);
            return buffer;
        }

        public static FixedArray<int> ConvertToUTF32(in FixedArray<byte> data, out YARGTextContainer<int> container)
        {
            const int UTF32BOM_OFFSET = 3;
            FixedArray<int> buffer;
            long length = (data.Length - UTF32BOM_OFFSET) / sizeof(int);
            if ((data[0] == 0xFF) != BitConverter.IsLittleEndian)
            {
                // We have to swap the endian of the data so string conversion works properly
                // but we can't just use the original buffer as we create a hash off it.
                buffer = FixedArray<int>.Alloc(length);
                for (int i = 0, j = UTF32BOM_OFFSET; i < buffer.Length; ++i, j += sizeof(int))
                {
                    buffer.Ptr[i] = data.Ptr[j] << 24 |
                                    data.Ptr[j + 1] << 16 |
                                    data.Ptr[j + 2] << 16 |
                                    data.Ptr[j + 3];
                }
            }
            else
            {
                buffer = FixedArray<int>.Cast(in data, UTF32BOM_OFFSET, length);
            }
            container = new YARGTextContainer<int>(in buffer, data[0] == 0xFF ? Encoding.UTF32 : UTF32BE);
            SkipPureWhitespace(ref container);
            return buffer;
        }

        public static void SkipPureWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            // Unity/Mono has a bug on the commented-out code here, where the JIT generates a useless
            // `cmp dword ptr [rax], 0` before actually performing ToInt32(null).
            // This causes an access violation (which translates to a NullReferenceException here) on
            // memory-mapped files whose size on disk is 2 or less bytes greater than the actual file contents,
            // due to the `cmp` above over-reading data from `rax` (which contains Position in that moment).
            //
            // Explicitly dereferencing the pointer into a value first avoids this issue. The useless `cmp`
            // is still generated, but now `rax` points to the stack, and so the over-read is always done in
            // a valid memory space.
            //
            // 9/28 Edit: However, now that fixedArray removed memorymappedfile functionality, the overread is a non-issue
            // in terms of causing any actual access violation errors
            while (container.Position < container.End && container.Position->ToInt32(null) <= 32)
            {
                ++container.Position;
            }
        }

        /// <summary>
        /// Skips all whitespace starting at the current position of the provided container,
        /// until the end of the current line.
        /// </summary>
        /// <remarks>"\n" is not included as whitespace in this version</remarks>
        /// <typeparam name="TChar">Type of data contained</typeparam>
        /// <param name="container">Buffer of data</param>
        /// <returns>The current character that halted skipping, or 0 if at EoF</returns>
        public static int SkipWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.End)
            {
                int ch = container.Position->ToInt32(null);
                if (ch > 32 || ch == '\n')
                {
                    return ch;
                }
                ++container.Position;
            }
            return (char) 0;
        }

        public static void SkipWhitespaceAndEquals<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (SkipWhitespace(ref container) == '=')
            {
                ++container.Position;
                SkipWhitespace(ref container);
            }
        }

        public static void GotoNextLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            var span = new ReadOnlySpan<TChar>(container.Position, (int) (container.End - container.Position));
            int index = span.IndexOf(TextConstants<TChar>.NEWLINE);
            if (index >= 0)
            {
                container.Position += index;
                SkipPureWhitespace(ref container);
            }
            else
            {
                container.Position = container.End;
            }
        }

        public static bool SkipLinesUntil<TChar>(ref YARGTextContainer<TChar> container, TChar stopCharacter)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            GotoNextLine(ref container);
            while (true)
            {
                var span = new ReadOnlySpan<TChar>(container.Position, (int) (container.End - container.Position));
                int i = span.IndexOf(stopCharacter);
                if (i == -1)
                {
                    container.Position = container.End;
                    return false;
                }

                var limit = container.Position;
                container.Position += i;

                var test = container.Position - 1;
                int character = test->ToInt32(null);
                while (test > limit && character <= 32 && character != '\n')
                {
                    --test;
                    character = test->ToInt32(null);
                }

                if (character == '\n')
                {
                    return true;
                }
                container.Position++;
            }
        }

        public static unsafe string ExtractModifierName<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            var start = container.Position;
            while (container.Position < container.End)
            {
                int b = container.Position->ToInt32(null);
                if (b <= 32 || b == '=')
                {
                    break;
                }
                ++container.Position;
            }

            string name = Decode(start, container.Position - start, ref container.Encoding);
            SkipWhitespaceAndEquals(ref container);
            return name;
        }

        public static unsafe string PeekLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            var span = new ReadOnlySpan<TChar>(container.Position, (int) (container.End - container.Position));
            long length = span.IndexOf(TextConstants<TChar>.NEWLINE);
            if (length == -1)
            {
                length = span.Length;
            }
            return Decode(container.Position, length, ref container.Encoding).TrimEnd();
        }

        public static unsafe string ExtractText<TChar>(ref YARGTextContainer<TChar> container, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            var stringBegin = container.Position;
            TChar* stringEnd = null;
            if (isChartFile && container.Position < container.End && container.Position->ToInt32(null) == '\"')
            {
                while (true)
                {
                    ++container.Position;
                    if (container.Position == container.End)
                    {
                        break;
                    }

                    int ch = container.Position->ToInt32(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (stringEnd == null)
                    {
                        if (ch == '\"' && container.Position[-1].ToInt32(null) != '\\')
                        {
                            ++stringBegin;
                            stringEnd = container.Position;
                        }
                        else if (ch == '\r')
                        {
                            stringEnd = container.Position;
                        }
                    }
                }
            }
            else
            {
                while (container.Position < container.End)
                {
                    int ch = container.Position->ToInt32(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (ch == '\r' && stringEnd == null)
                    {
                        stringEnd = container.Position;
                    }
                    ++container.Position;
                }
            }

            if (stringEnd == null)
            {
                stringEnd = container.Position;
            }

            while (stringBegin < stringEnd && stringEnd[-1].ToInt32(null) <= 32)
                --stringEnd;

            return Decode(stringBegin, stringEnd - stringBegin, ref container.Encoding);
        }

        public static bool ExtractBoolean<TChar>(in YARGTextContainer<TChar> text)
            where TChar : unmanaged, IConvertible
        {
            return text.Position < text.End && text.Position->ToInt32(null) switch
            {
                '0' => false,
                '1' => true,
                _ => text.Position + 4 <= text.End &&
                    (text.Position[0].ToInt32(null) is 't' or 'T') &&
                    (text.Position[1].ToInt32(null) is 'r' or 'R') &&
                    (text.Position[2].ToInt32(null) is 'u' or 'U') &&
                    (text.Position[3].ToInt32(null) is 'e' or 'E'),
            };
        }

        /// <summary>
        /// Extracts a short and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The short</returns>
        public static short ExtractInt16AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractInt16(ref container, out short value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a ushort and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ushort</returns>
        public static ushort ExtractUInt16AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractUInt16(ref container, out ushort value))
            {
                throw new Exception("Data for UInt16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a int and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The int</returns>
        public static int ExtractInt32AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractInt32(ref container, out int value))
            {
                throw new Exception("Data for Int32 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a uint and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The uint</returns>
        public static uint ExtractUInt32AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractUInt32(ref container, out uint value))
            {
                throw new Exception("Data for UInt32 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a long and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The long</returns>
        public static long ExtractInt64AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractInt64(ref container, out long value))
            {
                throw new Exception("Data for Int64 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a ulong and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ulong</returns>
        public static ulong ExtractUInt64AndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractUInt64(ref container, out ulong value))
            {
                throw new Exception("Data for UInt64 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a float and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The float</returns>
        public static float ExtractFloatAndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractFloat(ref container, out float value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a double and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The double</returns>
        public static double ExtractDoubleAndWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtractDouble(ref container, out double value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public static bool TryExtractInt16<TChar>(ref YARGTextContainer<TChar> text, out short value)
            where TChar : unmanaged, IConvertible
        {
            bool result = InternalExtractSigned(ref text, out long tmp, short.MaxValue, short.MinValue, SHORT_MAX);
            value = (short) tmp;
            return result;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public static bool TryExtractInt32<TChar>(ref YARGTextContainer<TChar> text, out int value)
            where TChar : unmanaged, IConvertible
        {
            bool result = InternalExtractSigned(ref text, out long tmp, int.MaxValue, int.MinValue, INT_MAX);
            value = (int) tmp;
            return result;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public static bool TryExtractInt64<TChar>(ref YARGTextContainer<TChar> text, out long value)
            where TChar : unmanaged, IConvertible
        {
            return InternalExtractSigned(ref text, out value, long.MaxValue, long.MinValue, LONG_MAX);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public static bool TryExtractUInt16<TChar>(ref YARGTextContainer<TChar> text, out ushort value)
            where TChar : unmanaged, IConvertible
        {
            bool result = InternalExtractUnsigned(ref text, out ulong tmp, ushort.MaxValue, USHORT_MAX);
            value = (ushort) tmp;
            return result;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public static bool TryExtractUInt32<TChar>(ref YARGTextContainer<TChar> text, out uint value)
            where TChar : unmanaged, IConvertible
        {
            bool result = InternalExtractUnsigned(ref text, out ulong tmp, uint.MaxValue, UINT_MAX);
            value = (uint) tmp;
            return result;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public static bool TryExtractUInt64<TChar>(ref YARGTextContainer<TChar> text, out ulong value)
            where TChar : unmanaged, IConvertible
        {
            return InternalExtractUnsigned(ref text, out value, ulong.MaxValue, ULONG_MAX);
        }

        public static bool TryExtractFloat<TChar>(ref YARGTextContainer<TChar> text, out float value)
            where TChar : unmanaged, IConvertible
        {
            bool result = TryExtractDouble(ref text, out double tmp);
            value = (float) tmp;
            return result;
        }

        public static bool TryExtractDouble<TChar>(ref YARGTextContainer<TChar> text, out double value)
            where TChar : unmanaged, IConvertible
        {
            value = 0;
            if (text.Position >= text.End)
            {
                return false;
            }

            int ch = text.Position->ToInt32(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++text.Position;
                if (text.Position >= text.End)
                {
                    return false;
                }
                ch = text.Position->ToInt32(null);
            }

            if (ch < '0' || '9' < ch && ch != '.')
            {
                return false;
            }

            while ('0' <= ch && ch <= '9')
            {
                value *= 10;
                value += ch - '0';
                ++text.Position;
                if (text.Position == text.End)
                {
                    break;
                }
                ch = text.Position->ToInt32(null);
            }

            if (ch == '.')
            {
                ++text.Position;
                if (text.Position < text.End)
                {
                    double divisor = 1;
                    ch = text.Position->ToInt32(null);
                    while ('0' <= ch && ch <= '9')
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++text.Position;
                        if (text.Position == text.End)
                        {
                            break;
                        }
                        ch = text.Position->ToInt32(null);
                    }
                }
            }

            value *= sign;
            return true;
        }

        private static string Decode<TChar>(TChar* data, long count, ref Encoding encoding)
            where TChar : unmanaged, IConvertible
        {
            while (true)
            {
                try
                {
                    return encoding.GetString((byte*) data, (int) (count * sizeof(TChar)));
                }
                catch
                {
                    if (encoding != UTF8Strict)
                    {
                        throw;
                    }
                    encoding = Latin1;
                }
            }
        }

        private static void SkipDigits<TChar>(ref YARGTextContainer<TChar> text)
           where TChar : unmanaged, IConvertible
        {
            while (text.Position < text.End)
            {
                int ch = text.Position->ToInt32(null);
                if (ch < '0' || '9' < ch)
                {
                    break;
                }
                ++text.Position;
            }
        }

        private static bool InternalExtractSigned<TChar>(ref YARGTextContainer<TChar> text, out long value, long hardMax, long hardMin, long softMax)
            where TChar : unmanaged, IConvertible
        {
            value = 0;
            if (text.Position >= text.End)
            {
                return false;
            }

            int ch = text.Position->ToInt32(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++text.Position;
                    if (text.Position >= text.End)
                    {
                        return false;
                    }
                    ch = text.Position->ToInt32(null);
                    break;
            }

            if (ch < '0' || '9' < ch)
            {
                return false;
            }

            while (true)
            {
                value += ch - '0';

                ++text.Position;
                if (text.Position < text.End)
                {
                    ch = text.Position->ToInt32(null);
                    if ('0' <= ch && ch <= '9')
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_SIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = sign == -1 ? hardMin : hardMax;
                        SkipDigits(ref text);
                        return true;
                    }
                }

                value *= sign;
                return true;
            }
        }

        private static bool InternalExtractUnsigned<TChar>(ref YARGTextContainer<TChar> text, out ulong value, ulong hardMax, ulong softMax)
            where TChar : unmanaged, IConvertible
        {
            value = 0;
            if (text.Position >= text.End)
            {
                return false;
            }

            int ch = text.Position->ToInt32(null);
            if (ch == '+')
            {
                ++text.Position;
                if (text.Position >= text.End)
                {
                    return false;
                }
                ch = text.Position->ToInt32(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            while (true)
            {
                value += (ulong) (ch - '0');

                ++text.Position;
                if (text.Position < text.End)
                {
                    ch = text.Position->ToInt32(null);
                    if ('0' <= ch && ch <= '9')
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_UNSIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = hardMax;
                        SkipDigits(ref text);
                    }
                }
                break;
            }
            return true;
        }
    }
}
