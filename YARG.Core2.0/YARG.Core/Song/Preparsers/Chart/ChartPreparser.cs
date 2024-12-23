using System;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class ChartPreparser
    {
        /// <returns>Whether the track was fully traversed</returns>
        public static unsafe bool Traverse<TChar>(ref YARGTextContainer<TChar> container, Difficulty difficulty, ref PartValues scan, delegate*<int, bool> func)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (scan[difficulty])
                return false;

            DotChartEvent ev = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    int lane = YARGTextReader.ExtractInt32AndWhitespace(ref container);
                    long _ = YARGTextReader.ExtractInt64AndWhitespace(ref container);
                    if (func(lane))
                    {
                        scan.SetDifficulty(difficulty);
                        return false;
                    }
                }
            }
            return true;
        }

        private const int KEYS_MAX = 5;
        private const int GUITAR_FIVEFRET_MAX = 5;
        private const int OPEN_NOTE = 7;
        private const int SIX_FRET_BLACK1 = 8;

        // Uses FiveFret parsing rules, but leaving this here just in case.
        //public static bool ValidateKeys(int lane)
        //{
        //    return lane < KEYS_MAX;
        //}

        public static bool ValidateSixFret(int lane)
        {
            return lane < GUITAR_FIVEFRET_MAX || lane == SIX_FRET_BLACK1 || lane == OPEN_NOTE;
        }

        public static bool ValidateFiveFret(int lane)
        {
            return lane < GUITAR_FIVEFRET_MAX || lane == OPEN_NOTE;
        }
    }
}