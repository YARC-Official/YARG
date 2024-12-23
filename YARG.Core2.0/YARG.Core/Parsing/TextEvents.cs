using System;
using YARG.Core.Extensions;

namespace YARG.Core.Parsing
{
    /// <summary>
    /// Available stem configurations for a drums mix event.
    /// </summary>
    public enum DrumsMixConfiguration
    {
        StereoKit = 0,
        NoStem = StereoKit,
        MonoKickSnare_StereoKit = 1,
        MonoKick_StereoSnareKit = 2,
        StereoKickSnareKit = 3,
        MonoKick_StereoKit = 4,
        StereoKickSnareTomCymbal = 5,
    }

    /// <summary>
    /// Additional settings for a drums mix event.
    /// </summary>
    public enum DrumsMixSetting
    {
        None = 0,

        /// <summary>
        /// Swap the red and yellow lanes on Pro Drums, along with their assigned stems.
        /// </summary>
        DiscoFlip = 1,

        /// <summary>
        /// Swap the stems assigned to the red/yellow lanes without swapping the notes.
        /// </summary>
        DiscoNoFlip = 2,

        /// <summary>
        /// Force-unmute the tom and cymbal stems on Easy.
        /// </summary>
        Easy = 3,

        /// <summary>
        /// Force-unmute the kick stem on Easy.
        /// </summary>
        EasyNoKick = 4,
    }

    /// <summary>
    /// Constants and utilities for handling text events.
    /// </summary>
    public static partial class TextEvents
    {
        public const string BIG_ROCK_ENDING_START = "coda";
        public const string END_MARKER = "end";

        /// <summary>
        /// Normalizes text events into a consistent format. This includes stripping any
        /// leading/trailing whitespace, and isolating any text inside square brackets.
        /// </summary>
        /// <remarks>
        /// All text events must be passed through this method before being used elsewhere.
        /// All other methods that operate on text events expect them to be normalized.
        /// </remarks>
        // Equivalent to reading the capture of this regex: \[(.*?)\]
        public static ReadOnlySpan<char> NormalizeTextEvent(ReadOnlySpan<char> text, out bool hadBrackets)
        {
            hadBrackets = text.Length > 0 && text[0] == '[' && text[^1] == ']';
            if (hadBrackets)
            {
                // Remove brackets
                text = text[1..(text.Length - 1)];
            }
            return text;
        }

        /// <inheritdoc cref="NormalizeTextEvent(ReadOnlySpan{char}, out bool)"/>
        public static ReadOnlySpan<char> NormalizeTextEvent(ReadOnlySpan<char> text)
            => NormalizeTextEvent(text, out _);

        // For events that have either space or underscore separators
        private static ReadOnlySpan<char> SkipSpaceOrUnderscore(this ReadOnlySpan<char> text)
        {
            return text.TrimStart('_').TrimStart();
        }

        /// <summary>
        /// Parses a section name from a text event.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        // Equivalent to reading the capture of this regex: (?:section|prc)[ _](.*)
        public static bool TryParseSectionEvent(ReadOnlySpan<char> text, out ReadOnlySpan<char> name)
        {
            name = ReadOnlySpan<char>.Empty;

            const string SECTION_PREFIX = "section";
            const string PRC_PREFIX = "prc";

            // Remove event prefix
            if (text.StartsWith(SECTION_PREFIX))
                text = text[SECTION_PREFIX.Length..];
            else if (text.StartsWith(PRC_PREFIX))
                text = text[PRC_PREFIX.Length..];
            else
                return false;

            // Isolate section name
            name = text.TrimStart('_').Trim();
            return !name.IsEmpty;
        }

        /// <summary>
        /// Parses mix info from a drums mix event.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        public static bool TryParseDrumsMixEvent(ReadOnlySpan<char> text, out Difficulty difficulty,
            out DrumsMixConfiguration config, out DrumsMixSetting setting)
        {
            difficulty = Difficulty.Expert;
            config = DrumsMixConfiguration.NoStem;
            setting = DrumsMixSetting.None;

            // Remove event prefix
            const string MIX_PREFIX = "mix";
            if (!text.StartsWith(MIX_PREFIX))
                return false;
            text = text[MIX_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse difficulty number
            if (!text[0].TryAsciiToNumber(out uint diffNumber))
                return false;
            text = text[1..].SkipSpaceOrUnderscore();

            switch (diffNumber)
            {
                case 3: difficulty = Difficulty.Expert; break;
                case 2: difficulty = Difficulty.Hard; break;
                case 1: difficulty = Difficulty.Medium; break;
                case 0: difficulty = Difficulty.Easy; break;
                default: return false;
            }

            // Skip 'drums' text
            const string DRUMS_PREFIX = "drums";
            if (!text.StartsWith(DRUMS_PREFIX))
                return false;
            text = text[DRUMS_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse configuration number
            if (!text[0].TryAsciiToNumber(out uint configNumber) || configNumber > 5)
                return false;
            text = text[1..].SkipSpaceOrUnderscore();

            config = (DrumsMixConfiguration) configNumber;

            // Parse settings
            var settingText = text;
            if (settingText.Equals("d", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoFlip;
            else if (settingText.Equals("dnoflip", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoNoFlip;
            else if (settingText.Equals("easy", StringComparison.Ordinal))
                setting = DrumsMixSetting.Easy;
            else if (settingText.Equals("easynokick", StringComparison.Ordinal))
                setting = DrumsMixSetting.EasyNoKick;
            else
                setting = DrumsMixSetting.None;

            return true;
        }
    }
}