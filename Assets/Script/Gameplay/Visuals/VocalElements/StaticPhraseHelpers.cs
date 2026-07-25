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
        public const  string PRESENT_LYRIC_COLOR_TAG            = "<color=#13f0a6>";
        public const  string FUTURE_LYRIC_COLOR_TAG             = "<color=#FFFFFF>";
        public const  string FUTURE_STAR_POWER_LYRIC_COLOR_TAG  = "<color=#FFEB04>";
        public const  string FUTURE_PHRASE_COLOR_TAG            = "<color=#595959>";
        public const  string FUTURE_STAR_POWER_PHRASE_COLOR_TAG = "<color=#757519>";
        public const  string CLOSE_COLOR_TAG                    = "</color>";

        public const double IMMINENCE_THRESHOLD = .3d;
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
                    builder.Append(text[0..^1]);
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
    }
}