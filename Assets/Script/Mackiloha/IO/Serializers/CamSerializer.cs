using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class CamSerializer : AbstractSerializer
    {
        public CamSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }
        
        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var cam = data as Cam;
            int version = ReadMagic(ar, data);

            MiloSerializer.ReadFromStream(ar.BaseStream, cam.Trans);
            MiloSerializer.ReadFromStream(ar.BaseStream, cam.Draw);

            cam.NearPlane = ar.ReadSingle();
            cam.FarPlane = ar.ReadSingle();
            cam.FOV = ar.ReadSingle();

            // Read screen area
            cam.ScreenArea = new Rectangle()
            {
                X = ar.ReadSingle(),
                Y = ar.ReadSingle(),
                Width = ar.ReadSingle(),
                Height = ar.ReadSingle()
            };

            // Read z-range
            cam.ZRange = new Vector2()
            {
                X = ar.ReadSingle(),
                Y = ar.ReadSingle()
            };

            cam.TargetTexture = ar.ReadString();
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var cam = data as Cam;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);

            MiloSerializer.WriteToStream(aw.BaseStream, cam.Trans);
            MiloSerializer.WriteToStream(aw.BaseStream, cam.Draw);

            aw.Write((float)cam.NearPlane);
            aw.Write((float)cam.FarPlane);
            aw.Write((float)cam.FOV);

            // Write screen area
            aw.Write((float)cam.ScreenArea.X);
            aw.Write((float)cam.ScreenArea.Y);
            aw.Write((float)cam.ScreenArea.Width);
            aw.Write((float)cam.ScreenArea.Height);

            // Write z-range
            aw.Write((float)cam.ZRange.X);
            aw.Write((float)cam.ZRange.Y);

            aw.Write((string)cam.TargetTexture);
        }

        public override bool IsOfType(ISerializable data) => data is Cam;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 9;
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
                    return new[] { 9 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
