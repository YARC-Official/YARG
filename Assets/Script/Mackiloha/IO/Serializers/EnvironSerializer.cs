using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class EnvironSerializer : AbstractSerializer
    {
        public EnvironSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }
        
        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var env = data as Environ;
            int version = ReadMagic(ar, data);

            MiloSerializer.ReadFromStream(ar.BaseStream, env.Draw);

            // Read lights
            var lightCount = ar.ReadInt32();
            env.Lights.Clear();
            env.Lights.AddRange(RepeatFor(lightCount, () => ar.ReadString()));

            env.AmbientColor = new Color4()
            {
                R = ar.ReadSingle(),
                G = ar.ReadSingle(),
                B = ar.ReadSingle(),
                A = ar.ReadSingle()
            };

            env.FogStart = ar.ReadSingle();
            env.FogEnd = ar.ReadSingle();

            env.FogColor = new Color4()
            {
                R = ar.ReadSingle(),
                G = ar.ReadSingle(),
                B = ar.ReadSingle(),
                A = ar.ReadSingle()
            };

            env.EnableFog = ar.ReadBoolean();
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var env = data as Environ;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);

            MiloSerializer.WriteToStream(aw.BaseStream, env.Draw);

            // Write lights
            aw.Write((int)env.Lights.Count);
            env.Lights.ForEach(x => aw.Write((string)x));

            // Write ambient color
            aw.Write((float)env.AmbientColor.R);
            aw.Write((float)env.AmbientColor.G);
            aw.Write((float)env.AmbientColor.B);
            aw.Write((float)env.AmbientColor.A);

            // Write fog info
            aw.Write((float)env.FogStart);
            aw.Write((float)env.FogEnd);

            aw.Write((float)env.FogColor.R);
            aw.Write((float)env.FogColor.G);
            aw.Write((float)env.FogColor.B);
            aw.Write((float)env.FogColor.A);

            aw.Write((bool)env.EnableFog);
        }

        public override bool IsOfType(ISerializable data) => data is Environ;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 1;
                default:
                    return -1;
            }
        }

        internal override int[] ValidMagics()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return new[] { 1 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
