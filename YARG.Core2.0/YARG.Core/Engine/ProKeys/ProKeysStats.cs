using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Replays;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        public ProKeysStats()
        {
        }

        public ProKeysStats(ProKeysStats stats) : base(stats)
        {
            Overhits = stats.Overhits;
        }

        public ProKeysStats(UnmanagedMemoryStream stream, int version)
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
            return new ProKeysReplayStats(name, this);
        }
    }
}