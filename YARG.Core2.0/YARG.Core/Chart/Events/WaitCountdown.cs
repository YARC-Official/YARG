using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public class WaitCountdown : ChartEvent
    {
        public const float MIN_SECONDS = 9;
        public const uint MIN_MEASURES = 4;
        public const float MIN_MEASURE_LENGTH = 1;
        public const float FADE_ANIM_LENGTH = 0.45f;
        public const int END_COUNTDOWN_MEASURE = 1;

        public int TotalMeasures => _measureBeatlines.Count;

        //The time where the countdown should start fading out and overstrums will break combo again
        public double DeactivateTime => _measureBeatlines[^(END_COUNTDOWN_MEASURE + 1)].Time;
        public bool IsActive => MeasuresLeft > END_COUNTDOWN_MEASURE;

        private List<Beatline> _measureBeatlines;

        public int MeasuresLeft {get; private set; }

        public WaitCountdown(List<Beatline> measureBeatlines)
        {
            _measureBeatlines = measureBeatlines;

            var firstCountdownMeasure = measureBeatlines[0];
            var lastCountdownMeasure = measureBeatlines[^1];

            Time = firstCountdownMeasure.Time;
            Tick = firstCountdownMeasure.Tick;
            TimeLength = lastCountdownMeasure.Time - Time;
            TickLength = lastCountdownMeasure.Tick - Tick;

            MeasuresLeft = TotalMeasures;
        }

        public int CalculateMeasuresLeft(uint currentTick)
        {
            int newMeasuresLeft;
            if (currentTick >= TickEnd)
            {
                newMeasuresLeft = 0;
            }
            else if (currentTick < Tick)
            {
                newMeasuresLeft = TotalMeasures;
            }
            else
            {
                newMeasuresLeft = TotalMeasures - _measureBeatlines.GetIndexOfNext(currentTick);
            }

            MeasuresLeft = newMeasuresLeft;

            return newMeasuresLeft;
        }
    }
}