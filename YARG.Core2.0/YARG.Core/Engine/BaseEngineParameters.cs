using System.Globalization;
using System.IO;
using System.Linq;
using YARG.Core.Extensions;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters
    {
        public readonly HitWindowSettings HitWindow;

        public readonly int MaxMultiplier;

        public readonly double StarPowerWhammyBuffer;

        public readonly double SustainDropLeniency;

        public readonly float[] StarMultiplierThresholds;

        public double SongSpeed;

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            StarPowerWhammyBuffer = spWhammyBuffer;
            SustainDropLeniency = sustainDropLeniency;
            MaxMultiplier = maxMultiplier;
            StarMultiplierThresholds = starMultiplierThresholds;
        }

        protected BaseEngineParameters(UnmanagedMemoryStream stream, int version)
        {
            HitWindow = new HitWindowSettings(stream, version);
            MaxMultiplier = stream.Read<int>(Endianness.Little);
            StarPowerWhammyBuffer = stream.Read<double>(Endianness.Little);

            // Version 7 but DATA_MIN was increased so no need to version check
            SustainDropLeniency = stream.Read<double>(Endianness.Little);

            // Read star multiplier thresholds
            int count = stream.Read<int>(Endianness.Little);
            StarMultiplierThresholds = new float[count];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarMultiplierThresholds[i] = stream.Read<float>(Endianness.Little);
            }

            SongSpeed = stream.Read<double>(Endianness.Little);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            HitWindow.Serialize(writer);
            writer.Write(MaxMultiplier);
            writer.Write(StarPowerWhammyBuffer);

            writer.Write(SustainDropLeniency);

            // Write star multiplier thresholds
            writer.Write(StarMultiplierThresholds.Length);
            foreach (var f in StarMultiplierThresholds)
            {
                writer.Write(f);
            }

            writer.Write(SongSpeed);
        }

        public override string ToString()
        {
            var thresholds = string.Join(", ",
                StarMultiplierThresholds.Select(i => i.ToString(CultureInfo.InvariantCulture)));

            return
                $"Hit window: ({HitWindow.MinWindow}, {HitWindow.MaxWindow})\n" +
                $"Hit window dynamic: {HitWindow.IsDynamic}\n" +
                $"Max multiplier: {MaxMultiplier}\n" +
                $"Star thresholds: {thresholds}";
        }
    }
}