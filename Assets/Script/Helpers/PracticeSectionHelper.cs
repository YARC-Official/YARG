using System;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Localization;

namespace YARG.Assets.Script.Helpers
{
    public static class PracticeSectionHelper
    {
        public static string ParseSectionName(string sectionName)
        {
            // Best-effort attempt to convert [section]-based practice sections to the expected format
            sectionName = sectionName.ToLower().Replace(' ', '_').Replace("guitar", "gtr");

            // Handle letter-based sections like "A section" (prc_a) and "B section 3" (prc_b3)
            if (IsLetterBasedSectionName(sectionName))
            {
                var letterBasedName = Localize.KeyFormat(
                    "Gameplay.Practice.SectionFormats.LetterSection",
                    char.ToUpper(sectionName[0])
                );

                if (sectionName.Length == 1)
                {
                    return letterBasedName;
                }

                return Localize.KeyFormat(
                    "Gameplay.Practice.SectionFormats.WithNumber",
                    letterBasedName,
                    sectionName[1..].TrimStart('_')
                );
            }

            (var name, var number) = DeriveNameAndNumber(sectionName);

            var key = Localize.MakeKey("Gameplay.Practice.Sections", name);
            var localizedName = Localize.Key(key);

            if (localizedName == key)
            {
                // No localization for the section name, so return unlocalized
                return sectionName.Replace('_', ' ');
            }

            if (number != null)
            {
                return Localize.KeyFormat("Gameplay.Practice.SectionFormats.WithNumber", localizedName, number);
            }

            return localizedName;
        }

        /**
         * Whether the string follows the convention for letter-based section names
         */
        private static bool IsLetterBasedSectionName(string text)
        {
            if (text.Length == 0 || !text[0].IsAsciiLetterLower())
            {
                return false;
            }

            if (text.Length == 1)
            {
                // e.g. "a" for "A section"
                return true;
            }

            if (text.Length == 2)
            {
                // e.g. "a1" for "A section 1"
                return text[1].IsAsciiDigit();
            }

            if (text[1] == '_')
            {
                // e.g. "a_b" for "A section B"
                return text.Length == 3 && text[2].IsAsciiLetterLower();
            }

            bool allAsciiDigit = true;
            foreach (var c in text.AsSpan()[1..^1])
            {
                if (!c.IsAsciiDigit())
                {
                    allAsciiDigit = false;
                    break;
                }
            }

            if (allAsciiDigit)
            {
                if (text[^1].IsAsciiDigit())
                {
                    // e.g. "a123" for "A section 123"
                    return true;
                }

                if (text[^1].IsAsciiLetterLower())
                {
                    // e.g. "a123b" for "A section 123B"
                    return true;
                }
            }

            return false;
        }

        private static (string name, string number) DeriveNameAndNumber(string section)
        {
            if (section.Contains('_') && !section.EndsWith('_'))
            {
                var indexOfLastUnderscore = section.LastIndexOf('_');
                var lastWord = section[(indexOfLastUnderscore + 1)..];
                if (IsSectionNumber(lastWord))
                {
                    var name = section[0..indexOfLastUnderscore];

                    string normalizedSectionName;
                    return (_alternateSectionNames.TryGetValue(name, out normalizedSectionName) ? normalizedSectionName : name, lastWord);
                }
            }

            string normalizedName;
            return (_alternateSectionNames.TryGetValue(section, out normalizedName) ? normalizedName : section, null);
        }

        // Whether the string follows the convention for section numbers (a, 1, 1a)
        private static bool IsSectionNumber(string text)
        {
            if (text.Length == 0)
            {
                return false;
            }

            if (text.Length == 1)
            {
                return text[0].IsAsciiLetterLower() || text[0].IsAsciiDigit();
            }

            bool allAsciiDigit = true;
            foreach (var c in text.AsSpan()[..^2])
            {
                if (!c.IsAsciiDigit())
                {
                    allAsciiDigit = false;
                }
            }

            return allAsciiDigit && (text[^1].IsAsciiLetterLower() || text[^1].IsAsciiDigit());
        }

        private static Dictionary<string, string> _alternateSectionNames = new()
        {
            { "ah!", "ah" },
            { "big_rock_ending", "bre" },
            { "big_rock_ending!", "bre" },
            { "buildup", "build_up" },
            { "build-up", "build_up" },
            { "fadein", "fade_in" },
            { "fadeout", "fade_out" },
            { "fade-in", "fade_in" },
            { "fade-out", "fade_out" },
            { "high_melody", "hi_melody" },
            { "keyb_enters", "keyboard_enters" },
            { "kick_it!", "kick it" },
            { "low_melody", "lo_melody" },
            { "oohs_and_ahs", "oohs" },
            { "perc_solo", "percussion_solo" },
            { "pre_chorus", "prechorus" },
            { "pre-chorus", "prechorus" },
            { "pre_verse", "preverse" },
            { "pre-verse", "preverse" },
            { "sctrach_break", "scratch_break"},
            { "syth_enters", "synth_enters" },
            { "yeah!", "yeah" }
        };
    }
}
