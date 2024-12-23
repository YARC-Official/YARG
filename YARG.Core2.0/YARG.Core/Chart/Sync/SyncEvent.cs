namespace YARG.Core.Chart
{
    public abstract class SyncEvent : ChartEvent
    {
        public SyncEvent(double time, uint tick) : base(time, 0, tick, 0)
        {
        }
    }
}