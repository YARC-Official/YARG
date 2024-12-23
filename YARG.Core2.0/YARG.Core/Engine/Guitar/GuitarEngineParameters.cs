using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {
        public readonly double HopoLeniency;
        public readonly double StrumLeniency;
        public readonly double StrumLeniencySmall;
        public readonly bool InfiniteFrontEnd;
        public readonly bool AntiGhosting;

        public GuitarEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double hopoLeniency, double strumLeniency,
            double strumLeniencySmall, bool infiniteFrontEnd, bool antiGhosting)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            HopoLeniency = hopoLeniency;

            StrumLeniency = strumLeniency;
            StrumLeniencySmall = strumLeniencySmall;

            InfiniteFrontEnd = infiniteFrontEnd;
            AntiGhosting = antiGhosting;
        }

        public GuitarEngineParameters(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            HopoLeniency = stream.Read<double>(Endianness.Little);

            StrumLeniency = stream.Read<double>(Endianness.Little);
            StrumLeniencySmall = stream.Read<double>(Endianness.Little);

            InfiniteFrontEnd = stream.ReadBoolean();
            AntiGhosting = stream.ReadBoolean();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(HopoLeniency);

            writer.Write(StrumLeniency);
            writer.Write(StrumLeniencySmall);

            writer.Write(InfiniteFrontEnd);
            writer.Write(AntiGhosting);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Infinite front-end: {InfiniteFrontEnd}\n" +
                $"Anti-ghosting: {AntiGhosting}\n" +
                $"Hopo leniency: {HopoLeniency}\n" +
                $"Strum leniency: {StrumLeniency}\n" +
                $"Strum leniency (small): {StrumLeniencySmall}\n" +
                $"Star power whammy buffer: {StarPowerWhammyBuffer}";
        }
    }
}