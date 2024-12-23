using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine.Guitar;
using YARG.Core.Extensions;

namespace YARG.Core.Replays
{
    public sealed class GuitarReplayStats : ReplayStats
    {
        public readonly int TotalNotes;
        public readonly int NumNotesHit;
        public readonly float PercentageHit;
        public readonly int Overstrums;
        public readonly int GhostInputs;
        public readonly int SoloBonuses;

        public GuitarReplayStats(string name, GuitarStats stats)
            : base(name, stats)
        {
            TotalNotes = stats.TotalNotes;
            NumNotesHit = stats.NotesHit;
            Overstrums = stats.Overstrums;
            GhostInputs = stats.GhostInputs;
            SoloBonuses = stats.SoloBonuses;

            PercentageHit = 100.0f * NumNotesHit / TotalNotes;
        }

        public GuitarReplayStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            TotalNotes = stream.Read<int>(Endianness.Little);
            NumNotesHit = stream.Read<int>(Endianness.Little);
            Overstrums = stream.Read<int>(Endianness.Little);
            GhostInputs = stream.Read<int>(Endianness.Little);
            SoloBonuses = stream.Read<int>(Endianness.Little);

            PercentageHit = 100.0f * NumNotesHit / TotalNotes;
        }

        public override void Serialize(BinaryWriter writer)
        {
            // Five fret & six fret are interchangeable here
            writer.Write((byte) GameMode.FiveFretGuitar);
            base.Serialize(writer);
            writer.Write(TotalNotes);
            writer.Write(NumNotesHit);
            writer.Write(Overstrums);
            writer.Write(GhostInputs);
            writer.Write(SoloBonuses);
        }
    }
}
