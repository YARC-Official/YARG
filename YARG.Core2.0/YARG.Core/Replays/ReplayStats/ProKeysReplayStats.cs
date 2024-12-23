using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Extensions;

namespace YARG.Core.Replays
{
    public sealed class ProKeysReplayStats : ReplayStats
    {
        public readonly int TotalNotes;
        public readonly int NumNotesHit;
        public readonly float PercentageHit;
        public readonly int Overhits;
        public readonly int SoloBonuses;

        public ProKeysReplayStats(string name, ProKeysStats stats)
            : base(name, stats)
        {
            TotalNotes = stats.TotalNotes;
            NumNotesHit = stats.NotesHit;
            Overhits = stats.Overhits;
            SoloBonuses = stats.SoloBonuses;

            PercentageHit = 100.0f * NumNotesHit / TotalNotes;
        }

        public ProKeysReplayStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            TotalNotes = stream.Read<int>(Endianness.Little);
            NumNotesHit = stream.Read<int>(Endianness.Little);
            Overhits = stream.Read<int>(Endianness.Little);
            SoloBonuses = stream.Read<int>(Endianness.Little);

            PercentageHit = 100.0f * NumNotesHit / TotalNotes;
        }


        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte) GameMode.ProKeys);
            base.Serialize(writer);
            writer.Write(TotalNotes);
            writer.Write(NumNotesHit);
            writer.Write(Overhits);
            writer.Write(SoloBonuses);
        }
    }
}
