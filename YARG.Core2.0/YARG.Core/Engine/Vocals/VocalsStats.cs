using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Replays;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsStats : BaseStats
    {
        /// <summary>
        /// The amount of note ticks that was hit by the vocalist.
        /// </summary>
        public uint TicksHit;

        /// <summary>
        /// The amount of note ticks that were missed by the vocalist.
        /// </summary>
        public uint TicksMissed;

        /// <summary>
        /// The total amount of note ticks.
        /// </summary>
        public uint TotalTicks => TicksHit + TicksMissed;

        public override float Percent => TotalTicks == 0 ? 1f : (float) TicksHit / TotalTicks;

        public VocalsStats()
        {
        }

        public VocalsStats(VocalsStats stats) : base(stats)
        {
            TicksHit = stats.TicksHit;
            TicksMissed = stats.TicksMissed;
        }

        public VocalsStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            TicksHit = stream.Read<uint>(Endianness.Little);
            TicksMissed = stream.Read<uint>(Endianness.Little);
        }

        public override void Reset()
        {
            base.Reset();
            TicksHit = 0;
            TicksMissed = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(TicksHit);
            writer.Write(TicksMissed);
        }

        public override ReplayStats ConstructReplayStats(string name)
        {
            return new VocalsReplayStats(name, this);
        }
    }
}