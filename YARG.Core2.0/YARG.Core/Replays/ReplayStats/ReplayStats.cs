using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;

namespace YARG.Core.Replays
{
    public abstract class ReplayStats
    {
        public readonly string PlayerName;
        public readonly int Score;
        public readonly float Stars;
        public readonly int TotalOverdrivePhrases;
        public readonly int NumOverdrivePhrasesHit;
        public readonly int NumOverdriveActivations;
        public readonly float AverageMultiplier;
        public readonly int NumPauses;

        protected ReplayStats(string name, BaseStats stats)
        {
            PlayerName = name;
            Score = stats.TotalScore;
            Stars = stats.Stars;
            TotalOverdrivePhrases = stats.TotalStarPowerPhrases;
            NumOverdrivePhrasesHit = TotalOverdrivePhrases - stats.StarPowerPhrasesMissed;
            NumOverdriveActivations = stats.StarPowerActivationCount;
            AverageMultiplier = 0;
            NumPauses = 0;
        }

        protected ReplayStats(UnmanagedMemoryStream stream, int version)
        {
            PlayerName = stream.ReadString();
            Score = stream.Read<int>(Endianness.Little);
            Stars = stream.Read<float>(Endianness.Little);
            TotalOverdrivePhrases = stream.Read<int>(Endianness.Little);
            NumOverdrivePhrasesHit = stream.Read<int>(Endianness.Little);
            NumOverdriveActivations = stream.Read<int>(Endianness.Little);
            AverageMultiplier = stream.Read<float>(Endianness.Little);
            NumPauses = stream.Read<int>(Endianness.Little);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerName);
            writer.Write(Score);
            writer.Write(Stars);
            writer.Write(TotalOverdrivePhrases);
            writer.Write(NumOverdrivePhrasesHit);
            writer.Write(NumOverdriveActivations);
            writer.Write(AverageMultiplier);
            writer.Write(NumPauses);
        }
    }
}
