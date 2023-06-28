using YARG.Util;
using YARG.PlayMode;

namespace YARG.Data
{
    public abstract class AbstractInfo
    {
        public float time;
        public float length;

        public float EndTime => time + length;

        // TODO: set lengthInBeats at construction time
        private float? _lengthInBeats = null;

        public float LengthInBeats
        {
            get
            {
                if (_lengthInBeats == null)
                {
                    _lengthInBeats = Utils.InfoLengthInBeats(this, Play.Instance.chart.beats);
                }

                return (float) _lengthInBeats;
            }
        }
    }
}