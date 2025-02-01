using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Helpers
{
    // TODO: Figure out a better place to put this
    public class FiveFretRangeShift
    {
        public double Time         { get; private set; }
        public int    Range        { get; private set; }
        public int    Size         { get; private set; }
        public double BeatDuration { get; set; }

        public bool   Shown = false;

        private FiveFretRangeShift(double time, int range, int size)
        {
            Time = time;
            Range = range;
            Size = size;
        }

        public static List<FiveFretRangeShift> GetRangeShiftEvents(List<TextEvent> textEvents, Difficulty playerDifficulty)
        {
            var shiftEvents = new List<FiveFretRangeShift>();
            var eventDifficulty = (int?) GetRangeDifficultyForChartDifficulty(playerDifficulty);

            // We can't do anything with this, so log an error and return an empty list
            if (eventDifficulty == null)
            {
                YargLogger.LogFormatDebug("Unable to find range shift difficulty for chart difficulty {0}", playerDifficulty);
                return shiftEvents;
            }

            foreach (var textEvent in textEvents)
            {
                if (!textEvent.Text.StartsWith("ld_range_shift"))
                {
                    continue;
                }

                // Validate the event and add it to the list
                // My natural inclination here would be to use a regex, but they're not really used
                // anywhere else in YARG, so...
                var splitEvent = textEvent.Text.Split(' ');
                if (splitEvent.Length != 4)
                {
                    YargLogger.LogFormatDebug("Unable to parse range shift event: {0}", textEvent.Text);
                    continue;
                }

                // TODO: Consider merging this with the above, though I feel like this way is more readable
                if (!(int.TryParse(splitEvent[1], out int difficulty) &&
                      int.TryParse(splitEvent[2], out int range) &&
                      int.TryParse(splitEvent[3], out int size)))
                {
                    YargLogger.LogFormatDebug("Unable to parse range shift event: {0}", textEvent.Text);
                    continue;
                }

                // Further validation
                // 1) Is the range index valid
                if (range < 1 || range > 5)
                {
                    YargLogger.LogFormatDebug("Invalid range index in range shift event: {0}", textEvent.Text);
                    continue;
                }

                // 2) Is the size valid for the range index
                if (range + size > 6)
                {
                    YargLogger.LogFormatDebug("Invalid range size (too large for index) in shift event: {0}", textEvent.Text);
                    continue;
                }

                // TODO: Range probably needs to be adjusted since visual frets are zero indexed (I think)
                //  and the chart fret is one indexed since zero is an open note

                if (difficulty == eventDifficulty)
                {
                    shiftEvents.Add(new FiveFretRangeShift(textEvent.Time, range, size));
                }
            }
            return shiftEvents;
        }

        private static RangeDifficulty? GetRangeDifficultyForChartDifficulty(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy   => RangeDifficulty.Easy,
                Difficulty.Medium => RangeDifficulty.Medium,
                Difficulty.Hard   => RangeDifficulty.Hard,
                Difficulty.Expert => RangeDifficulty.Expert,
                _                 => null,
            };
        }

        private enum RangeDifficulty
        {
            Easy   = 0,
            Medium  = 1,
            Hard   = 2,
            Expert  = 3,
        }
    }
}