using System;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.IO;

namespace YARG.Core.Replays
{
    public class ReplayFrame
    {
        private static readonly FourCC FRAME_TAG = new('R', 'P', 'F', 'M');

        public readonly YargProfile          Profile;
        public readonly BaseEngineParameters EngineParameters;
        public readonly BaseStats            Stats;
        public readonly GameInput[]          Inputs;

        public int InputCount => Inputs.Length;

        public ReplayFrame(YargProfile profile, BaseEngineParameters param, BaseStats stats, GameInput[] inputs)
        {
            Profile = profile;
            Stats = stats;
            EngineParameters = param;
            Inputs = inputs;
        }

        public ReplayFrame(UnmanagedMemoryStream stream, int version)
        {
            if (!FRAME_TAG.Matches(stream))
            {
                throw new Exception("RPFM tag not found");
            }

            Profile = new YargProfile(stream);
            switch (Profile.CurrentInstrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    EngineParameters = new GuitarEngineParameters(stream, version);
                    Stats = new GuitarStats(stream, version);
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    EngineParameters = new DrumsEngineParameters(stream, version);
                    Stats = new DrumsStats(stream, version);
                    break;
                case GameMode.Vocals:
                    EngineParameters = new VocalsEngineParameters(stream, version);
                    Stats = new VocalsStats(stream, version);
                    break;
                case GameMode.ProKeys:
                    EngineParameters = new ProKeysEngineParameters(stream, version);
                    Stats = new ProKeysStats(stream, version);
                    break;
                default:
                    throw new InvalidOperationException("Stat creation not implemented.");
            }

            int count = stream.Read<int>(Endianness.Little);
            Inputs = new GameInput[count];
            for (int i = 0; i < count; i++)
            {
                double time = stream.Read<double>(Endianness.Little);
                int action = stream.Read<int>(Endianness.Little);
                int value = stream.Read<int>(Endianness.Little);

                Inputs[i] = new GameInput(time, action, value);
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            FRAME_TAG.Serialize(writer);
            Profile.Serialize(writer);
            EngineParameters.Serialize(writer);
            Stats.Serialize(writer);

            writer.Write(InputCount);
            for (int i = 0; i < InputCount; i++)
            {
                writer.Write(Inputs[i].Time);
                writer.Write(Inputs[i].Action);
                writer.Write(Inputs[i].Integer);
            }
        }
    }
}