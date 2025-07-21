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
        public int    Position     { get; private set; }
        public int    Size         { get; private set; }
        public double BeatDuration { get; set; }

        public bool   Shown = false;

        private FiveFretRangeShift(double time, int position, int size)
        {
            Time = time;
            Position = position;
            Size = size;
        }

        public static FiveFretRangeShift[] GetRangeShiftEvents(
            InstrumentDifficulty<GuitarNote> instrumentDifficulty)
        {
            var shiftEvents = instrumentDifficulty.RangeShiftEvents;
            FiveFretRangeShift[] shifts = new FiveFretRangeShift[shiftEvents.Count];


            for (int i = 0; i < shifts.Length; i++)
            {
                shifts[i] = new FiveFretRangeShift(shiftEvents[i].Time, shiftEvents[i].Range, shiftEvents[i].Size);
            }

            return shifts;
        }
    }
}