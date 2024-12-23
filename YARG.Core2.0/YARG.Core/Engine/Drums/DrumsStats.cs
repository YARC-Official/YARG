using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Replays;

namespace YARG.Core.Engine.Drums
{
    public class DrumsStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        public DrumsStats()
        {
        }

        public DrumsStats(DrumsStats stats) : base(stats)
        {
            Overhits = stats.Overhits;
        }

        public DrumsStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            Overhits = stream.Read<int>(Endianness.Little);
        }

        public override void Reset()
        {
            base.Reset();
            Overhits = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Overhits);
        }

        public override ReplayStats ConstructReplayStats(string name)
        {
            return new DrumsReplayStats(name, this);
        }
    }
}