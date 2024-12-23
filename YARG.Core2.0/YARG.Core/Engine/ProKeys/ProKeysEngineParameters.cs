using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineParameters : BaseEngineParameters
    {
        public readonly double ChordStaggerWindow;

        public readonly double FatFingerWindow;

        public ProKeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double chordStaggerWindow, double fatFingerWindow)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            ChordStaggerWindow = chordStaggerWindow;
            FatFingerWindow = fatFingerWindow;
        }

        public ProKeysEngineParameters(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            ChordStaggerWindow = stream.Read<double>(Endianness.Little);
            FatFingerWindow = stream.Read<double>(Endianness.Little);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(ChordStaggerWindow);
            writer.Write(FatFingerWindow);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Chord stagger window: {ChordStaggerWindow}\n" +
                $"Fat finger window: {FatFingerWindow}";
        }
    }
}