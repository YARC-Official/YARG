using System;
using System.Collections.Generic;
using Cysharp.Text;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public static class StaticPhraseHelpers
    {
        public const  string PAST_LYRIC_COLOR_TAG               = "<color=#595959>";
        public const  string PAST_STAR_POWER_LYRIC_COLOR_TAG    = "<color=#757519>";
        public const  string PRESENT_LYRIC_COLOR_TAG            = "<color=#0ea3f9>";
        public const  string FUTURE_LYRIC_COLOR_TAG             = "<color=#FFFFFF>";
        public const  string FUTURE_STAR_POWER_LYRIC_COLOR_TAG  = "<color=#FFEB04>";
        public const  string FUTURE_PHRASE_COLOR_TAG            = "<color=#595959>";
        public const  string FUTURE_STAR_POWER_PHRASE_COLOR_TAG = "<color=#757519>";
        public const  string CLOSE_COLOR_TAG                    = "</color>";

        public const double SMALL_GAP_THRESHOLD = .3d; // time > this = small gap
        public const double LARGE_GAP_THRESHOLD = 3 * SMALL_GAP_THRESHOLD; // time > this = large gap

        public static double GetTimeBetweenLyrics(VocalsPhrase next, VocalsPhrase curr)
        {
            return next.Lyrics[0].Time - Math.Min(curr.Lyrics[^1].TimeEnd, curr.TimeEnd);
        }
        public readonly struct StaticLyricSyllable
        {
            public readonly string Text;
            public readonly double Time;
            public readonly double TimeEnd;
            public readonly bool   IsStarpower;

            public StaticLyricSyllable(string text, double time, double timeEnd, bool isStarpower,
                LyricSymbolFlags flags, bool isLastLyricOfPhrase)
            {
                var builder = ZString.CreateStringBuilder(false);

                Time = time;
                TimeEnd = timeEnd;
                IsStarpower = isStarpower;

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("<i>");
                }

                if ((flags & LyricSymbolFlags.JoinWithNext) != 0)
                {
                    builder.Append(text[..^1]);
                }
                else
                {
                    builder.Append(text);
                }

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("</i>");
                }

                if (!isLastLyricOfPhrase && (flags & LyricSymbolFlags.JoinWithNext) == 0 &&
                    (flags & LyricSymbolFlags.HyphenateWithNext) == 0)
                {
                    builder.Append(" ");
                }

                Text = builder.ToString();
            }
        }
        public static void BuilderAppendWithColorTag(string text, string colorTag, ref Utf16ValueStringBuilder builder)
        {
            builder.Append(colorTag);
            builder.Append(text);
            builder.Append(CLOSE_COLOR_TAG);
        }

        public static void AddToRenderState(List<StaticLyricSyllable> syllables, double visualTime, ref HashCode hash)
        {
            for (int i = 0; i < syllables.Count; i++)
            {
                var syllable = syllables[i];
                int state = 2; // syllable is already hit (gray)

                if (visualTime < syllable.Time)
                {
                    state = 0; // syllable is in current phrase (active/white)
                }
                else if (visualTime < syllable.TimeEnd)
                {
                    state = 1; // syllable is being hit (cyan)
                }

                hash.Add(state);

                if (state == 0)
                {
                    // We can reasonably assume if we run into a syllable that has not yet been hit,
                    // there is no change after that syllable.
                    break;
                }
            }
        }

        public static void AddSyllablesToBuilder(List<StaticLyricSyllable> syllables, double visualTime,
            ref Utf16ValueStringBuilder builder)
        {
            for (int i = 0; i < syllables.Count; i++)
            {
                var syllable = syllables[i];
                if (visualTime < syllable.Time)
                {
                    BuilderAppendWithColorTag(syllable.Text,
                        syllable.IsStarpower
                            ? FUTURE_STAR_POWER_LYRIC_COLOR_TAG
                            : FUTURE_LYRIC_COLOR_TAG, ref builder);
                }
                else if (syllable.Time <= visualTime && visualTime < syllable.TimeEnd)
                {
                    BuilderAppendWithColorTag(syllable.Text,
                        PRESENT_LYRIC_COLOR_TAG, ref builder);
                }
                else
                {
                    BuilderAppendWithColorTag(syllable.Text,
                        syllable.IsStarpower
                            ? PAST_STAR_POWER_LYRIC_COLOR_TAG
                            : PAST_LYRIC_COLOR_TAG, ref builder);
                }
            }
        }
    }
}