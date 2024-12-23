using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Engine.Drums
{
    public class DrumsEngineParameters : BaseEngineParameters
    {
        public enum DrumMode : byte
        {
            NonProFourLane,
            ProFourLane,
            FiveLane
        }

        /// <summary>
        /// What mode the inputs should be processed in.
        /// </summary>
        public readonly DrumMode Mode;

        //Ghost notes are below this threshold, Accent notes are above 1 - threshold
        public readonly float VelocityThreshold;

        // The maximum allowed time (seconds) between notes to use context-sensitive velocity scoring
        public readonly float SituationalVelocityWindow;

        public DrumsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            DrumMode mode)
            : base(hitWindow, maxMultiplier, 0, 0, starMultiplierThresholds)
        {
            Mode = mode;
            VelocityThreshold = 0.35f;
            SituationalVelocityWindow = 1.5f;
        }

        public DrumsEngineParameters(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            Mode = (DrumMode) stream.ReadByte();
            VelocityThreshold = stream.Read<float>(Endianness.Little);
            SituationalVelocityWindow = stream.Read<float>(Endianness.Little);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write((byte) Mode);
            writer.Write(VelocityThreshold);
            writer.Write(SituationalVelocityWindow);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Velocity threshold: {VelocityThreshold}\n" +
                $"Situational velocity window: {SituationalVelocityWindow}";
        }
    }
}