using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;

namespace YARG.Core.Replays
{
    public sealed class VocalsReplayStats : ReplayStats
    {
        public readonly int NumPhrases;
        public readonly int NumPerfectPhrases;

        public VocalsReplayStats(string name, VocalsStats stats)
            : base(name, stats)
        {
            NumPhrases = 0;
            NumPerfectPhrases = 0;
        }

        public VocalsReplayStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            NumPhrases = stream.Read<int>(Endianness.Little);
            NumPerfectPhrases = stream.Read<int>(Endianness.Little);
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte) GameMode.Vocals);
            base.Serialize(writer);
            writer.Write(NumPhrases);
            writer.Write(NumPerfectPhrases);
        }
    }
}
