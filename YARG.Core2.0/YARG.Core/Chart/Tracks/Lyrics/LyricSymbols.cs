using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Utility;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Flags for lyric events.
    /// </summary>
    [Flags]
    public enum LyricSymbolFlags
    {
        None = 0,

        JoinWithNext = 1 << 0,

        NonPitched = 1 << 1,
        LenientScoring = 1 << 2,
        // NonPitchedUnknown = 1 << 3, // Reserved for once '*' is figured out

        PitchSlide = 1 << 4,
        HarmonyHidden = 1 << 5,
        StaticShift = 1 << 6,
        RangeShift = 1 << 7,
    }

    /// <summary>
    /// Definitions and utilities for special symbols in lyric events.
    /// </summary>
    public static class LyricSymbols
    {
        /// <summary>Joins two lyrics together as a single word.</summary>
        /// <remarks>Displayed as-is in vocals, stripped in lyrics.</remarks>
        public const char LYRIC_JOIN_SYMBOL = '-';

        /// <summary>Joins two syllables together and stands in for a hyphen.</summary>
        /// <remarks>Replaced with a hyphen ('-') in vocals and lyrics.</remarks>
        public const char LYRIC_JOIN_HYPHEN_SYMBOL = '=';

        /// <summary>Connects two notes together with a slide from end-to-end.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char PITCH_SLIDE_SYMBOL = '+';


        /// <summary>Marks a note as non-pitched.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_SYMBOL = '#';

        /// <summary>Marks a note as non-pitched, with more generous scoring.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_LENIENT_SYMBOL = '^';

        /// <summary>Marks a note as non-pitched, but its exact function is unknown.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_UNKNOWN_SYMBOL = '*';


        /// <summary>Marks a point at which the vocals track should recalculate its range.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char RANGE_SHIFT_SYMBOL = '%';

        /// <summary>Marks an additional shift point for the static vocals display.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char STATIC_SHIFT_SYMBOL = '/';


        /// <summary>Hides a lyric from being displayed in Harmonies.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char HARMONY_HIDE_SYMBOL = '$';

        /// <summary>Indicate two lexical syllables are sung as a single phonetic syllable.</summary>
        /// <remarks>
        /// Does not join two lyrics together, it is used within a single syllable specifically.
        /// Replaced with '‿' in vocals, replaced with a space (' ') in lyrics.
        /// </remarks>
        public const char JOINED_SYLLABLE_SYMBOL = '§';

        /// <summary>Stands in for a space (' ').</summary>
        /// <remarks>Only intended for use in lyrics, but will also be replaced in vocals.</remarks>
        public const char SPACE_ESCAPE_SYMBOL = '_';

        /// <summary>Symbols which should be stripped from lyrics in vocals.</summary>
        public static readonly HashSet<char> VOCALS_STRIP_SYMBOLS = new()
        {
            PITCH_SLIDE_SYMBOL,
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
            RANGE_SHIFT_SYMBOL,
            STATIC_SHIFT_SYMBOL,
            HARMONY_HIDE_SYMBOL,

            // Don't display quotation marks in vocals
            '"',
        };

        /// <summary>Symbols which should be stripped from lyrics in the lyrics track.</summary>
        public static readonly HashSet<char> LYRICS_STRIP_SYMBOLS = new()
        {
            LYRIC_JOIN_SYMBOL,
            PITCH_SLIDE_SYMBOL,
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
            RANGE_SHIFT_SYMBOL,
            STATIC_SHIFT_SYMBOL,
            HARMONY_HIDE_SYMBOL,
        };

        /// <summary>Symbols which should be replaced with another in lyrics on vocals.</summary>
        public static readonly Dictionary<char, char> VOCALS_SYMBOL_REPLACEMENTS = new()
        {
            { LYRIC_JOIN_HYPHEN_SYMBOL,     '-' },
            { JOINED_SYLLABLE_SYMBOL,       '‿' },
            { SPACE_ESCAPE_SYMBOL,          ' ' },
        };

        /// <summary>Symbols which should be replaced with another in lyrics on the lyrics track.</summary>
        public static readonly Dictionary<char, char> LYRICS_SYMBOL_REPLACEMENTS = new()
        {
            { LYRIC_JOIN_HYPHEN_SYMBOL,  '-' },
            { JOINED_SYLLABLE_SYMBOL,    ' ' },
            { SPACE_ESCAPE_SYMBOL,       ' ' },
        };

        private static readonly Dictionary<string, string> VOCALS_STRIP_REPLACEMENTS
            = CreateStripReplacements(VOCALS_STRIP_SYMBOLS, VOCALS_SYMBOL_REPLACEMENTS);

        private static readonly Dictionary<string, string> LYRICS_STRIP_REPLACEMENTS
            = CreateStripReplacements(LYRICS_STRIP_SYMBOLS, LYRICS_SYMBOL_REPLACEMENTS);

        private static Dictionary<string, string> CreateStripReplacements(
            HashSet<char> strip, Dictionary<char, char> replace)
        {
            // Add strip characters first to ensure they don't mess with the replacements
            return strip.Select((c) => new KeyValuePair<char, char>(c, '\0'))
                .Concat(replace)
                .ToDictionary(
                    (pair) => pair.Key != '\0' ? pair.Key.ToString() : string.Empty,
                    (pair) => pair.Value != '\0' ? pair.Value.ToString() : string.Empty);
        }

        // Rich text tags allowed by Clone Hero
        // https://strikeline.myjetbrains.com/youtrack/issue/CH-226
        public const RichTextTags LYRICS_ALLOWED_TAGS = RichTextTags.Italics | RichTextTags.Bold |
            RichTextTags.Strikethrough | RichTextTags.Underline | RichTextTags.Superscript | RichTextTags.Subscript |
            RichTextTags.VerticalOffset | RichTextTags.Lowercase | RichTextTags.Uppercase | RichTextTags.SmallCaps |
            RichTextTags.CharSpace | RichTextTags.Color | RichTextTags.Monospace | RichTextTags.LineBreak;

        public static LyricSymbolFlags GetFlagForSymbol(char symbol) => symbol switch
        {
            LYRIC_JOIN_SYMBOL or
            LYRIC_JOIN_HYPHEN_SYMBOL => LyricSymbolFlags.JoinWithNext,

            PITCH_SLIDE_SYMBOL  => LyricSymbolFlags.PitchSlide,

            NONPITCHED_SYMBOL => LyricSymbolFlags.NonPitched,
            NONPITCHED_LENIENT_SYMBOL => LyricSymbolFlags.NonPitched | LyricSymbolFlags.LenientScoring,
            NONPITCHED_UNKNOWN_SYMBOL => LyricSymbolFlags.NonPitched, // | LyricSymbolFlags.NonPitchedUnknown,

            RANGE_SHIFT_SYMBOL => LyricSymbolFlags.RangeShift,

            STATIC_SHIFT_SYMBOL => LyricSymbolFlags.StaticShift,
            HARMONY_HIDE_SYMBOL => LyricSymbolFlags.HarmonyHidden,

            _ => LyricSymbolFlags.None
        };

        public static LyricSymbolFlags GetLyricFlags(ReadOnlySpan<char> lyric)
        {
            var flags = LyricSymbolFlags.None;

            if (lyric.IsEmpty)
                return flags;

            // Flags at the start of the lyric
            // Only the harmony hide symbol is valid here
            if (lyric[0] == HARMONY_HIDE_SYMBOL)
                flags |= LyricSymbolFlags.HarmonyHidden;

            // Flags at the end of the lyric
            for (; !lyric.IsEmpty; lyric = lyric[..^1])
            {
                var flag = GetFlagForSymbol(lyric[^1]);
                if (flag == LyricSymbolFlags.None)
                    break;

                flags |= flag;
            }

            return flags;
        }

        // Workaround for a certain set of badly-formatted vocal tracks which place the hyphen
        // for pitch bend lyrics on the pitch bend and not the lyric itself
        internal static void DeferredLyricJoinWorkaround(List<LyricEvent> lyrics, ref ReadOnlySpan<char> lyric, bool addHyphen)
        {
            if (lyrics.Count > 0 && !lyrics[^1].JoinWithNext &&
                (lyric.Equals("+-", StringComparison.Ordinal) || lyric.Equals("-+", StringComparison.Ordinal)))
            {
                var other = lyrics[^1];
                string text = addHyphen ? $"{other.Text}-" : other.Text;
                lyrics[^1] = new(other.Flags | LyricSymbolFlags.JoinWithNext, text, other.Time, other.Tick);
                lyric = "+";
            }
        }

        public static string StripForVocals(string lyric)
        {
            lyric = RichTextUtils.StripRichTextTags(lyric);

            var lyricBuffer = new StringBuilder(lyric);
            foreach (var (symbol, replacement) in VOCALS_STRIP_REPLACEMENTS)
            {
                lyricBuffer.Replace(symbol, replacement);
            }

            return lyricBuffer.ToString();
        }

        public static string StripForLyrics(string lyric)
        {
            lyric = RichTextUtils.StripRichTextTags(lyric, ~LYRICS_ALLOWED_TAGS);
            lyric = RichTextUtils.ReplaceColorNames(lyric);

            var lyricBuffer = new StringBuilder();
            var segmentBuffer = new StringBuilder();

            // Need to ensure rich text tags are not stripped
            int tagIndex;
            var remaining = lyric.AsSpan();
            while ((tagIndex = remaining.IndexOf('<')) >= 0)
            {
                // Split out segment before the tag
                var segment = remaining[..tagIndex];
                var tag = remaining[tagIndex..];

                // Find end of the tag
                int tagCloseIndex = tag.IndexOf('>');
                if (tagCloseIndex < 0)
                    break;

                // Include closing in tag split
                tagCloseIndex++;

                if (tagCloseIndex >= tag.Length)
                {
                    remaining = ReadOnlySpan<char>.Empty;
                }
                else
                {
                    remaining = tag[tagCloseIndex..];
                    tag = tag[..tagCloseIndex];
                }

                // Run through replacements on segment
                if (!segment.IsEmpty)
                {
                    segmentBuffer.Append(segment);
                    foreach (var (symbol, replacement) in LYRICS_STRIP_REPLACEMENTS)
                    {
                        segmentBuffer.Replace(symbol, replacement);
                    }

                    lyricBuffer.Append(segmentBuffer);
                    segmentBuffer.Clear();
                }

                // Insert tag unmodified
                lyricBuffer.Append(tag);
            }

            // Final segment of characters
            if (!remaining.IsEmpty)
            {
                segmentBuffer.Append(remaining);
                foreach (var (symbol, replacement) in LYRICS_STRIP_REPLACEMENTS)
                {
                    segmentBuffer.Replace(symbol, replacement);
                }

                lyricBuffer.Append(segmentBuffer);
            }

            return lyricBuffer.ToString();
        }
    }
}