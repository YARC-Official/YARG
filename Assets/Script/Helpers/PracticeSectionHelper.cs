using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using YARG.Core.Extensions;
using YARG.Localization;
using static UnityEngine.InputSystem.InputControlScheme.MatchResult;

namespace YARG.Assets.Script.Helpers
{
    public static class PracticeSectionHelper
    {
        public static string ParseSectionName(string sectionName)
        {
            var percentageMatch = Regex.Match(sectionName, "^ugc_section_(\\d\\d?)_(\\d\\d?)$");
            if (percentageMatch.Success)
            {
                var size = int.Parse(percentageMatch.Groups[1].Value);
                var start = int.Parse(percentageMatch.Groups[2].Value);

                return Localize.KeyFormat("Gameplay.Practice.SectionFormats.PercentageBased", start, start + size);
            }

            // Best-effort attempt to convert [section]-based practice sections to the expected format
            sectionName = sectionName.ToLower().Replace(' ', '_');

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
            foreach (var c in text.AsSpan()[..^1])
            {
                if (!c.IsAsciiDigit())
                {
                    allAsciiDigit = false;
                    break;
                }
            }

            return allAsciiDigit && (text[^1].IsAsciiLetterLower() || text[^1].IsAsciiDigit());
        }

        private static Dictionary<string, string> _alternateSectionNames = new()
        {
            { "aco_guitar_intro", "aco_gtr_intro" },
            { "acoustic_gtr_intro", "aco_gtr_intro" },
            { "acoustic_guitar_intro", "aco_gtr_intro" },
            { "ah!", "ah" },
            { "big_rock_ending", "bre" },
            { "big_rock_ending!", "bre" },
            { "buildup", "build_up" },
            { "build-up", "build_up" },
            { "fadein", "fade_in" },
            { "fadeout", "fade_out" },
            { "fade-in", "fade_in" },
            { "fade-out", "fade_out" },
            { "guitar_break", "gtr_break" },
            { "guitar_enters", "gtr_enters" },
            { "guitar_fill", "gtr_fill" },
            { "guitar_hook", "gtr_hook" },
            { "guitar_intro", "gtr_intro" },
            { "guitar_lead", "gtr_lead" },
            { "guitar_lick", "gtr_lick" },
            { "guitar_line", "gtr_line" },
            { "guitar_melody", "gtr_melody" },
            { "guitar_ostinato", "gtr_ostinato" },
            { "guitar_riff", "gtr_riff" },
            { "guitar_solo", "gtr_solo" },
            { "high_melody", "hi_melody" },
            { "keyb_break", "keyboard_break" },
            { "keyb_enters", "keyboard_enters" },
            { "keyb_intro", "keyboard_intro" },
            { "keyb_solo", "keyboard_solo" },
            { "kick_it!", "kick_it" },
            { "kybd_break", "keyboard_break" },
            { "kybd_enters", "keyboard_enters" },
            { "kybd_intro", "keyboard_intro" },
            { "kybd_solo", "keyboard_solo" },
            { "low_melody", "lo_melody" },
            { "oohs_and_ahs", "oohs" },
            { "orchestra_intro", "orch_intro" },
            { "orchestral_intro", "orch_intro" },
            { "perc_break", "percussion_break" },
            { "perc_solo", "percussion_solo" },
            { "post_chorus", "postchorus" },
            { "post-chorus", "postchorus" },
            { "post_verse", "postverse" },
            { "post-verse", "postverse" },
            { "pre_chorus", "prechorus" },
            { "pre-chorus", "prechorus" },
            { "pre_verse", "preverse" },
            { "pre-verse", "preverse" },
            { "rhy_gtr_enters", "rhy_enters" },
            { "rhy_guitar_enters", "rhy_enters" },
            { "rhythm_gtr_enters", "rhy_enters" },
            { "rhythm_guitar_enters", "rhy_enters" },
            { "sctrach_break", "scratch_break"},
            { "spacey", "spacey_part" },
            { "speed_picking", "fast_picking" },
            { "speedup", "speed_up" },
            { "speed-up", "speed_up" },
            { "syth_enters", "synth_enters" },
            { "yeah!", "yeah" }
        };
    }
}
