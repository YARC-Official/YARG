
namespace Haukcode.sACN.Model
{
    public class SendStatistics
    {
        public int DroppedPackets { get; set; }

        public int QueueLength { get; set; }

        public int DestinationCount { get; set; }

        public int SlowSends { get; set; }
    }
}
