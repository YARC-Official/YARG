using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public static unsafe class YARGDTAReader
    {
        public static YARGTextContainer<byte> TryCreate(in FixedArray<byte> data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                YargLogger.LogError("UTF-16 & UTF-32 are not supported for .dta files");
                return YARGTextContainer<byte>.Null;
            }

            var container = new YARGTextContainer<byte>(in data, YARGTextReader.Latin1);
            if (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                container.Position += 3;
                container.Encoding = Encoding.UTF8;
            }
            return container;
        }

        public static char SkipWhitespace(ref YARGTextContainer<byte> container)
        {
            while (container.Position < container.End)
            {
                char ch = (char) *container.Position;
                if (ch > 32 && ch != ';')
                {
                    return ch;
                }

                ++container.Position;
                if (ch > 32)
                {
                    // In comment
                    while (container.Position < container.End)
                    {
                        if (*container.Position++ == '\n')
                        {
                            break;
                        }
                    }
                }
            }
            return (char) 0;
        }

        public static string GetNameOfNode(ref YARGTextContainer<byte> container, bool allowNonAlphetical)
        {
            char ch = (char) container.CurrentValue;
            if (ch == '(')
            {
                return string.Empty;
            }

            bool hasApostrophe = ch == '\'';
            if (hasApostrophe)
            {
                ++container.Position;
                ch = (char) container.CurrentValue;
            }

            var start = container.Position;
            var end = container.Position;
            while (true)
            {
                if (ch == '\'')
                {
                    if (!hasApostrophe)
                    {
                        throw new Exception("Invalid name format");
                    }
                    container.Position = end + 1;
                    break;
                }

                if (ch <= 32)
                {
                    if (!hasApostrophe)
                    {
                        container.Position = end + 1;
                        break;
                    }
                }
                else if (!allowNonAlphetical && !ch.IsAsciiLetter() && ch != '_')
                {
                    container.Position = end;
                    break;
                }
                
                ++end;
                if (end >= container.End)
                {
                    container.Position = end;
                    break;
                }
                ch = (char) *end;
            }

            SkipWhitespace(ref container);
            return Encoding.UTF8.GetString(start, (int) (end - start));
        }

        private enum TextScopeState
        {
            None,
            Squirlies,
            Quotes,
            Apostrophes,
            Comment
        }

        public static string ExtractText(ref YARGTextContainer<byte> container)
        {
            char ch = (char) container.CurrentValue;
            var state = ch switch
            {
                '{' => TextScopeState.Squirlies,
                '\"' => TextScopeState.Quotes,
                '\'' => TextScopeState.Apostrophes,
                _ => TextScopeState.None
            };

            if (state != TextScopeState.None)
            {
                ++container.Position;
                ch = (char) container.CurrentValue;
            }

            var start = container.Position;
            // Loop til the end of the text is found
            while (true)
            {
                if (ch == '{')
                    throw new Exception("Text error - no { braces allowed");

                if (ch == '}')
                {
                    if (state == TextScopeState.Squirlies)
                        break;
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (state == TextScopeState.Quotes)
                        break;
                    if (state != TextScopeState.Squirlies)
                        throw new Exception("Text error - no quotes allowed");
                }
                else if (ch == '\'')
                {
                    if (state == TextScopeState.Apostrophes)
                        break;
                    if (state == TextScopeState.None)
                        throw new Exception("Text error - no apostrophes allowed");
                }
                else if (ch <= 32 || ch == ')')
                {
                    if (state == TextScopeState.None)
                        break;
                }
                ++container.Position;
                ch = (char) container.CurrentValue;
            }

            string txt = container.Encoding.GetString(start, (int) (container.Position - start)).Replace("\\q", "\"");
            if (ch != ')')
            {
                ++container.Position;

            }
            SkipWhitespace(ref container);
            return txt;
        }

        public static int[] ExtractArray_Int(ref YARGTextContainer<byte> container)
        {
            bool doEnd = StartNode(ref container);
            List<int> values = new();
            while (container.CurrentValue != ')')
            {
                values.Add(ExtractInt32(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return values.ToArray();
        }

        public static float[] ExtractArray_Float(ref YARGTextContainer<byte> container)
        {
            bool doEnd = StartNode(ref container);
            List<float> values = new();
            while (container.CurrentValue != ')')
            {
                values.Add(ExtractFloat(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return values.ToArray();
        }

        public static string[] ExtractArray_String(ref YARGTextContainer<byte> container)
        {
            bool doEnd = StartNode(ref container);
            List<string> strings = new();
            while (container.CurrentValue != ')')
            {
                strings.Add(ExtractText(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return strings.ToArray();
        }

        public static bool StartNode(ref YARGTextContainer<byte> container)
        {
            if (container.IsAtEnd() || !container.IsCurrentCharacter('('))
            {
                return false;
            }

            ++container.Position;
            SkipWhitespace(ref container);
            return true;
        }

        public static void EndNode(ref YARGTextContainer<byte> container)
        {
            int scopeLevel = 0;
            int squirlyCount = 0;
            var textState = TextScopeState.None;
            while (container.Position < container.End && scopeLevel >= 0)
            {
                char curr = (char) *container.Position;
                ++container.Position;
                if (textState == TextScopeState.Comment)
                {
                    if (curr == '\n')
                    {
                        textState = TextScopeState.None;
                    }
                }
                else if (curr == '{')
                {
                    if (textState != TextScopeState.None && textState != TextScopeState.Squirlies)
                    {
                        throw new Exception("Invalid open-squirly found!");
                    }
                    textState = TextScopeState.Squirlies;
                    ++squirlyCount;
                }
                else if (curr == '}')
                {
                    if (textState != TextScopeState.Squirlies)
                    {
                        throw new Exception("Invalid close-squirly found!");
                    }
                    --squirlyCount;
                    if (squirlyCount == 0)
                    {
                        textState = TextScopeState.None;
                    }
                }
                else if (curr == '\"')
                {
                    switch (textState)
                    {
                        case TextScopeState.Apostrophes:
                            throw new Exception("Invalid quotation mark found!");
                        case TextScopeState.None:
                            textState = TextScopeState.Quotes;
                            break;
                        case TextScopeState.Quotes:
                            textState = TextScopeState.None;
                            break;
                    }
                }
                else if (textState == TextScopeState.None)
                {
                    switch (curr)
                    {
                        case '(': ++scopeLevel; break;
                        case ')': --scopeLevel; break;
                        case '\'': textState = TextScopeState.Apostrophes; break;
                        case ';': textState = TextScopeState.Comment; break;
                    }
                }
                else if (textState == TextScopeState.Apostrophes && curr == '\'')
                {
                    textState = TextScopeState.None;
                }
            }
            SkipWhitespace(ref container);
        }

        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `false` on failed extraction</returns>
        public static bool ExtractBoolean(ref YARGTextContainer<byte> container)
        {
            bool result = YARGTextReader.ExtractBoolean(in container);
            SkipWhitespace(ref container);
            return result;
        }
        
        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `true` on failed extraction</returns>
        public static bool ExtractBoolean_FlippedDefault(ref YARGTextContainer<byte> container)
        {
            bool result = container.Position >= container.End || (char)*container.Position switch
            {
                '0' => false,
                '1' => true,
                _ => container.Position + 5 > container.End ||
                    char.ToLowerInvariant((char)container.Position[0]) != 'f' ||
                    char.ToLowerInvariant((char)container.Position[1]) != 'a' ||
                    char.ToLowerInvariant((char)container.Position[2]) != 'l' ||
                    char.ToLowerInvariant((char)container.Position[3]) != 's' ||
                    char.ToLowerInvariant((char)container.Position[4]) != 'e',
            };
            SkipWhitespace(ref container);
            return result;
        }

        /// <summary>
        /// Extracts a short and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The short</returns>
        public static short ExtractInt16(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractInt16(ref container, out short value))
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
        public static ushort ExtractUInt16(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractUInt16(ref container, out ushort value))
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
        public static int ExtractInt32(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractInt32(ref container, out int value))
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
        public static uint ExtractUInt32(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractUInt32(ref container, out uint value))
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
        public static long ExtractInt64(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractInt64(ref container, out long value))
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
        public static ulong ExtractUInt64(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractUInt64(ref container, out ulong value))
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
        public static float ExtractFloat(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractFloat(ref container, out float value))
            {
                throw new Exception("Data for float not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        /// <summary>
        /// Extracts a double and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The double</returns>
        public static double ExtractDouble(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtractDouble(ref container, out double value))
            {
                throw new Exception("Data for double not present");
            }
            SkipWhitespace(ref container);
            return value;
        }
    };
}
