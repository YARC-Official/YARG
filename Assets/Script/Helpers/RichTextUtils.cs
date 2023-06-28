using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace YARG.Util
{
    public static class RichTextUtils
    {
        private static readonly string[] RichTextTagStrings =
        {
            "align", "alpha", "color", "b", "i", "cspace", "font", "indent", "line-height", "line-indent", "link",
            "lowercase", "uppercase", "smallcaps", "margin", "mark", "mspace", "noparse", "nobr", "page", "pos", "size",
            "space", "sprite", "s", "u", "style", "sub", "sup", "voffset", "width", "br", "font-weight", "gradient",
            "rotate", "allcaps"
        };

        public const RichTextTags ALL_TAGS = (RichTextTags) ~(ulong) 0;
        public const RichTextTags GOOD_TAGS = ALL_TAGS & ~BAD_TAGS;

        public const RichTextTags BAD_TAGS = ALL_TAGS & ~(RichTextTags.Alpha | RichTextTags.Color | RichTextTags.Bold |
            RichTextTags.Italics | RichTextTags.LowerCase |
            RichTextTags.Uppercase | RichTextTags.SmallCaps |
            RichTextTags.Strikethrough | RichTextTags.Underline |
            RichTextTags.Subscript | RichTextTags.Superscript);

        private static readonly Dictionary<ulong, Regex> RegexCache = new();

        public static string StripRichTextTags(string text)
        {
            return StripRichTextTagsPrivate(text, ALL_TAGS);
        }

        public static string StripRichTextTags(string text, RichTextTags tags)
        {
            return StripRichTextTagsPrivate(text, tags);
        }

        public static string StripRichTextTagsExclude(string text, RichTextTags excludeTags)
        {
            return StripRichTextTagsPrivate(text, ALL_TAGS & ~excludeTags);
        }

        private static string StripRichTextTagsPrivate(string text, RichTextTags tags)
        {
            if (RegexCache.TryGetValue((ulong) tags, out var cachedRegex))
            {
                return cachedRegex.Replace(text, "");
            }

            var regexFormat = @"<\/*{0}.*?>|";

            var sb = new StringBuilder();
            int enumLength = Enum.GetValues(typeof(RichTextTags)).Length;
            for (int i = 0; i < enumLength; i++)
            {
                if ((tags & (RichTextTags) (1 << i)) != 0)
                {
                    sb.AppendFormat(regexFormat, RichTextTagStrings[i]);
                }
            }

            if (sb.Length > 0) regexFormat = sb.Remove(sb.Length - 1, 1).ToString();

            var regex = new Regex(regexFormat, RegexOptions.Compiled);
            RegexCache.Add((ulong) tags, regex);

            return regex.Replace(text, "");
        }
    }

    [Flags]
    public enum RichTextTags : ulong
    {
        Align = 1 << 0,
        Alpha = 1 << 1,
        Color = 1 << 2,
        Bold = 1 << 3,
        Italics = 1 << 4,
        CSpace = 1 << 5,
        Font = 1 << 6,
        Indent = 1 << 7,
        LineHeight = 1 << 8,
        LineIndent = 1 << 9,
        Link = 1 << 10,
        LowerCase = 1 << 11,
        Uppercase = 1 << 12,
        SmallCaps = 1 << 13,
        Margin = 1 << 14,
        Mark = 1 << 15,
        Monospace = 1 << 16,
        NoParsing = 1 << 17,
        NoSpaceBreak = 1 << 18,
        Page = 1 << 19,
        HorizontalPosition = 1 << 20,
        FontSize = 1 << 21,
        HorizontalSpace = 1 << 22,
        Sprite = 1 << 23,
        Strikethrough = 1 << 24,
        Underline = 1 << 25,
        Style = 1 << 26,
        Subscript = 1 << 27,
        Superscript = 1 << 28,
        BaselineOffset = 1 << 29,
        Width = 1 << 30,
        LineBreak = 1UL << 31,
        FontWeight = 1UL << 32,
        Gradient = 1UL << 33,
        Rotate = 1UL << 34,
        AllCaps = 1UL << 35
    }
}