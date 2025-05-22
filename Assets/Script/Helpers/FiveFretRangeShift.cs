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

        public static List<FiveFretRangeShift> GetRangeShiftEvents(
            InstrumentDifficulty<GuitarNote> instrumentDifficulty)
        {
            var shiftEvents = new List<FiveFretRangeShift>();

            foreach (var shift in instrumentDifficulty.RangeShiftEvents)
            {
                shiftEvents.Add(new FiveFretRangeShift(shift.Time, shift.Range, shift.Size));
            }

            return shiftEvents;
        }
    }
}