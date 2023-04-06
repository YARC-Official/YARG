using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class ViewSerializer : AbstractSerializer
    {
        public ViewSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var view = data as View;
            int version = ReadMagic(ar, data);
            var meta = ReadMeta(ar);
            
            MiloSerializer.ReadFromStream(ar.BaseStream, view.Anim);
            MiloSerializer.ReadFromStream(ar.BaseStream, view.Trans);
            MiloSerializer.ReadFromStream(ar.BaseStream, view.Draw);

            if(version >= 11)
            {
                // Read draw group
                var drawableCount = ar.ReadInt32();
                view.Draw.Drawables.Clear();
                view.Draw.Drawables.AddRange(RepeatFor(drawableCount, () => ar.ReadString()));
            }

            view.MainView = ar.ReadString();

            if (version == 11)
            {
                // Has less data at end of file
                return;
            }
            
            // Ratio is usually 4:3
            view.LODHeight = ar.ReadSingle();
            view.LODWidth = ar.ReadSingle();

            // 9 extra bytes for v14?

            /*
            if (view.ScreenHeight > 0.0f && (view.ScreenWidth / view.ScreenHeight) != (4.0f / 3.0f))
                throw new Exception($"Aspect ratio should be {(4.0f / 3.0f):F2}, got {(view.ScreenWidth / view.ScreenHeight):F2}");
            */
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var view = data as View;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);

            MiloSerializer.WriteToStream(aw.BaseStream, view.Anim);
            MiloSerializer.WriteToStream(aw.BaseStream, view.Trans);
            MiloSerializer.WriteToStream(aw.BaseStream, view.Draw);

            aw.Write((string)view.MainView);
            aw.Write((float)view.LODHeight);
            aw.Write((float)view.LODWidth);
        }

        public override bool IsOfType(ISerializable data) => data is View;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 7;
                case 24:
                    return 12;
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
                    return new[] { 7 };
                case 24:
                    // GH2
                    return new[] { 11, 12 };
                case 25:
                    // TBRB
                    return new[] { 14 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
