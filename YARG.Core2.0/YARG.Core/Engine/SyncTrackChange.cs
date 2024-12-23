using YARG.Core.Chart;

namespace YARG.Core.Engine
{
    public class SyncTrackChange : SyncEvent
    {
        public readonly int Index;

        public TempoChange Tempo;

        public TimeSignatureChange TimeSignature;

        public SyncTrackChange(int index, TempoChange tempo, TimeSignatureChange timeSig, double time, uint tick) : base(time, tick)
        {
            Index = index;
            Tempo = tempo;
            TimeSignature = timeSig;
        }
    }
}