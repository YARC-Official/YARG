using System;
using System.Collections.Generic;
using Cysharp.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Utility
{
    [Flags]
    public enum RichTextTags : ulong
    {
        None = 0,

        // Organized according to the text tags in alphabetical order
        /// <summary>The "align" tag.</summary>
        Align = 1UL << 0,
        /// <summary>The "allcaps" tag.</summary>
        AllCaps = 1UL << 1,
        /// <summary>The "alpha" tag.</summary>
        Alpha = 1UL << 2,
        /// <summary>The "b" tag.</summary>
        Bold = 1UL << 3,
        /// <summary>The "br" tag.</summary>
        LineBreak = 1UL << 4,
        /// <summary>The "color" tag.</summary>
        Color = 1UL << 5,
        /// <summary>The "cspace" tag.</summary>
        CharSpace = 1UL << 6,
        /// <summary>The "font" tag.</summary>
        Font = 1UL << 7,
        /// <summary>The "font-weight" tag.</summary>
        FontWeight = 1UL << 8,
        /// <summary>The "gradient" tag.</summary>
        Gradient = 1UL << 9,
        /// <summary>The "i" tag.</summary>
        Italics = 1UL << 10,
        /// <summary>The "indent" tag.</summary>
        Indent = 1UL << 11,
        /// <summary>The "line-height" tag.</summary>
        LineHeight = 1UL << 12,
        /// <summary>The "line-indent" tag.</summary>
        LineIndent = 1UL << 13,
        /// <summary>The "link" tag.</summary>
        Link = 1UL << 14,
        /// <summary>The "lowercase" tag.</summary>
        Lowercase = 1UL << 15,
        /// <summary>The "margin" tag.</summary>
        Margin = 1UL << 16,
        /// <summary>The "mark" tag.</summary>
        Mark = 1UL << 17,
        /// <summary>The "mspace" tag.</summary>
        Monospace = 1UL << 18,
        /// <summary>The "noparse" tag.</summary>
        NoParse = 1UL << 19,
        /// <summary>The "nobr" tag.</summary>
        NoBreak = 1UL << 20,
        /// <summary>The "page" tag.</summary>
        PageBreak = 1UL << 21,
        /// <summary>The "pos" tag.</summary>
        HorizontalPosition = 1UL << 22,
        /// <summary>The "rotate" tag.</summary>
        Rotate = 1UL << 23,
        /// <summary>The "size" tag.</summary>
        FontSize = 1UL << 24,
        /// <summary>The "smallcaps" tag.</summary>
        SmallCaps = 1UL << 25,
        /// <summary>The "space" tag.</summary>
        HorizontalSpace = 1UL << 26,
        /// <summary>The "sprite" tag.</summary>
        Sprite = 1UL << 27,
        /// <summary>The "s" tag.</summary>
        Strikethrough = 1UL << 28,
        /// <summary>The "style" tag.</summary>
        Style = 1UL << 29,
        /// <summary>The "sub" tag.</summary>
        Subscript = 1UL << 30,
        /// <summary>The "sup" tag.</summary>
        Superscript = 1UL << 31,
        /// <summary>The "u" tag.</summary>
        Underline = 1UL << 32,
        /// <summary>The "uppercase" tag.</summary>
        Uppercase = 1UL << 33,
        /// <summary>The "voffset" tag.</summary>
        VerticalOffset = 1UL << 34,
        /// <summary>The "width" tag.</summary>
        Width = 1UL << 35,

        MaxBit = Width,

        AllTags = ~0UL,

        /// <summary>Tags which are acceptable for general purposes.</summary>
        GoodTags = Alpha | Color | Bold | Italics | Lowercase | Uppercase |
            SmallCaps | Strikethrough | Underline | Subscript | Superscript,

        /// <summary>Tags which are not desirable for general purposes.</summary>
        BadTags = ~GoodTags,
    }

    public static class RichTextUtils
    {
        static RichTextUtils() { }
        public static readonly (string Text, RichTextTags tag)[] RICH_TEXT_TAGS =
        {
            ( "align",       RichTextTags.Align),
            ( "allcaps",     RichTextTags.AllCaps),
            ( "alpha",       RichTextTags.Alpha),
            ( "b",           RichTextTags.Bold),
            ( "br",          RichTextTags.LineBreak),
            ( "color",       RichTextTags.Color),
            ( "cspace",      RichTextTags.CharSpace),
            ( "font",        RichTextTags.Font),
            ( "font-weight", RichTextTags.FontWeight),
            ( "gradient",    RichTextTags.Gradient),
            ( "i",           RichTextTags.Italics),
            ( "indent",      RichTextTags.Indent),
            ( "line-height", RichTextTags.LineHeight),
            ( "line-indent", RichTextTags.LineIndent),
            ( "link",        RichTextTags.Link),
            ( "lowercase",   RichTextTags.Lowercase),
            ( "margin",      RichTextTags.Margin),
            ( "mark",        RichTextTags.Mark),
            ( "mspace",      RichTextTags.Monospace),
            ( "noparse",     RichTextTags.NoParse),
            ( "nobr",        RichTextTags.NoBreak),
            ( "page",        RichTextTags.PageBreak),
            ( "pos",         RichTextTags.HorizontalPosition),
            ( "rotate",      RichTextTags.Rotate),
            ( "size",        RichTextTags.FontSize),
            ( "smallcaps",   RichTextTags.SmallCaps),
            ( "space",       RichTextTags.HorizontalSpace),
            ( "sprite",      RichTextTags.Sprite),
            ( "s",           RichTextTags.Strikethrough),
            ( "style",       RichTextTags.Style),
            ( "sub",         RichTextTags.Subscript),
            ( "sup",         RichTextTags.Superscript),
            ( "u",           RichTextTags.Underline),
            ( "uppercase",   RichTextTags.Uppercase),
            ( "voffset",     RichTextTags.VerticalOffset),
            ( "width",       RichTextTags.Width),
        };

        private static readonly Dictionary<RichTextTags, string[]> STRIP_CACHE = new();
        public static string StripRichTextTags(string text, RichTextTags excludeTags = RichTextTags.AllTags)
        {
            string[] tags = GetStripList(excludeTags);

            Span<char> buffer = stackalloc char[text.Length];
            int length = 0;

            var span = text.AsSpan();
            for (int position = 0, nextPosition; position < text.Length; position = nextPosition)
            {
                if (!ParseHTMLBounds(text, position, out int open, out int end))
                {
                    if (position == 0)
                    {
                        return text;
                    }
                    nextPosition = end = text.Length;
                }
                else
                {
                    nextPosition = ++end;
                    var tag = span[open..end];
                    foreach (var tagText in tags)
                    {
                        if (tag.StartsWith(tagText))
                        {
                            end = open;
                            break;
                        }
                    }
                }

                while (position < end)
                {
                    buffer[length++] = text[position++];
                }
            }
            return length < text.Length ? new string(buffer[0..length]) : text;
        }

        private static (string Original, string Replacement)[] COLOR_TO_HEX_LIST =
        {
            ( "aqua",      "<color=#00ffff>" ),
            ( "black",     "<color=#000000>" ),
            ( "blue",      "<color=#0000ff>" ),
            ( "brown",     "<color=#a52a2a>" ),
            ( "cyan",      "<color=#00ffff>" ),
            ( "darkblue",  "<color=#0000a0>" ),
            ( "fuchsia",   "<color=#ff00ff>" ),
            ( "green",     "<color=#008000>" ),
            ( "grey",      "<color=#808080>" ),
            ( "lightblue", "<color=#add8e6>" ),
            ( "lime",      "<color=#00ff00>" ),
            ( "magenta",   "<color=#ff00ff>" ),
            ( "maroon",    "<color=#800000>" ),
            ( "navy",      "<color=#000080>" ),
            ( "olive",     "<color=#808000>" ),
            ( "orange",    "<color=#ffa500>" ),
            ( "purple",    "<color=#800080>" ),
            ( "red",       "<color=#ff0000>" ),
            ( "silver",    "<color=#c0c0c0>" ),
            ( "teal",      "<color=#008080>" ),
            ( "white",     "<color=#ffffff>" ),
            ( "yellow",    "<color=#ffff00>" ),
        };

        public static string ReplaceColorNames(string text)
        {
            using var builder = ZString.CreateStringBuilder(notNested: true);
            var span = text.AsSpan();
            for (int position = 0, nextPosition; position < span.Length; position = nextPosition)
            {
                if (!ParseHTMLBounds(text, position, out int open, out int close))
                {
                    if (position == 0)
                    {
                        return text;
                    }
                    builder.Append(span[position..text.Length]);
                    break;
                }

                nextPosition = close + 1;

                bool found = false;
                var tag = span[(open + 1)..close];
                if (tag.StartsWith("color="))
                {
                    tag = tag[6..].TrimOnce('"');
                    foreach (var (original, replacement) in COLOR_TO_HEX_LIST)
                    {
                        if (tag.SequenceEqual(original))
                        {
                            builder.Append(span[position..open]);
                            builder.Append(replacement);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    builder.Append(span[position..(close + 1)]);
                }
            }
            return builder.ToString();
        }

        private static bool ParseHTMLBounds(string text, int position, out int open, out int close)
        {
            open = text.IndexOf('<', position);
            if (open == -1)
            {
                close = -1;
                return false;
            }

            close = text.IndexOf('>', open);
            return close != -1;
        }

        private static string[] GetStripList(RichTextTags excludeTags)
        {
            string[] tags;
            lock (STRIP_CACHE)
            {
                if (!STRIP_CACHE.TryGetValue(excludeTags, out tags))
                {
                    var list = new List<string>(RICH_TEXT_TAGS.Length * 3);
                    foreach (var (tagText, tag) in RICH_TEXT_TAGS)
                    {
                        if ((excludeTags & tag) != 0)
                        {
                            // Intern saves the string to the system pool, saving on memory
                            list.Add(string.Intern($"<{tagText}="));
                            list.Add(string.Intern($"<{tagText}>"));
                            list.Add(string.Intern($"</{tagText}>"));
                        }
                    }
                    STRIP_CACHE.Add(excludeTags, tags = list.ToArray());
                }
            }
            return tags;
        }
    }
}