using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class DrawSerializer : AbstractSerializer
    {
        public DrawSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var draw = data as Draw;
            int version = ReadMagic(ar, data);

            draw.Showing = ar.ReadBoolean();

            if (version < 3)
            {
                var drawableCount = ar.ReadInt32();
                draw.Drawables.Clear();
                draw.Drawables.AddRange(RepeatFor(drawableCount, () => ar.ReadString()));
            }

            draw.Boundry = new Sphere()
            {
                X = ar.ReadSingle(),
                Y = ar.ReadSingle(),
                Z = ar.ReadSingle(),
                Radius = ar.ReadSingle()
            };

            if (version == 3)
            {
                // Should always be 0
                ar.BaseStream.Position += 4;
            }
            else if (version >= 4)
            {
                // Should always be 0'd data
                ar.BaseStream.Position += 8;
            }
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var draw = data as Draw;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);

            aw.Write((bool)draw.Showing);

            aw.Write((int)draw.Drawables.Count);
            draw.Drawables.ForEach(x => aw.Write((string)x));

            // Write boundry
            aw.Write(draw.Boundry.X);
            aw.Write(draw.Boundry.Y);
            aw.Write(draw.Boundry.Z);
            aw.Write(draw.Boundry.Radius);
        }

        public override bool IsOfType(ISerializable data) => data is Draw;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 1;
                case 24:
                    // GH2
                    return 3;
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
                case 24:
                    // GH2
                    return new[] { 3 };
                case 25:
                    // TBRB
                    return new[] { 3, 4 /* GDRB */ };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
