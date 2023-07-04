using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay;

namespace YARG.Data
{
    public abstract class AbstractInfo
    {
        public float time;
        public float length;

        public float EndTime => time + length;

        public float GetLengthInBeats(YargChart chart) => GetLengthInBeats(chart.beats);

        public float GetLengthInBeats(List<Beat> beatTimes)
        {
            int beatIndex = 1;
            // set beatIndex to first relevant beat
            while (beatIndex < beatTimes.Count && beatTimes[beatIndex].Time <= time)
            {
                ++beatIndex;
            }

            float beats = 0;
            // add segments of the length wrt tempo
            for (; beatIndex < beatTimes.Count && beatTimes[beatIndex].Time <= EndTime; ++beatIndex)
            {
                var curBPS = 1 / (beatTimes[beatIndex].Time - beatTimes[beatIndex - 1].Time);
                // Unit math: s * b/s = pt
                beats += (beatTimes[beatIndex].Time - Mathf.Max(beatTimes[beatIndex - 1].Time, time)) * curBPS;
            }

            // segment where EndTime is between two beats (beatIndex-1 and beatIndex)
            if (beatIndex < beatTimes.Count && beatTimes[beatIndex - 1].Time < EndTime &&
                EndTime < beatTimes[beatIndex].Time)
            {
                var bps = 1 / (beatTimes[beatIndex].Time - beatTimes[beatIndex - 1].Time);
                beats += (EndTime - beatTimes[beatIndex - 1].Time) * bps;
            }
            // segment where EndTime is BEYOND the final beat
            else if (EndTime > beatTimes[^1].Time)
            {
                var bps = 1 / (beatTimes[^1].Time - beatTimes[^2].Time);
                var toAdd = (EndTime - beatTimes[^1].Time) * bps;
                beats += toAdd;
            }

            return beats;
        }
    }
}