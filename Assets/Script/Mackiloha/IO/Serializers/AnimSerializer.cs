using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class AnimSerializer : AbstractSerializer
    {
        public AnimSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var anim = data as Anim;
            int version = ReadMagic(ar, data);

            if (version >= 4)
            {
                // Skips anim rate + unknown
                ar.BaseStream.Position += 8;
                return;
            }

            // Read anim entries
            int count = ar.ReadInt32();
            anim.AnimEntries.Clear();
            anim.AnimEntries.AddRange(
                RepeatFor(count, () => new AnimEntry()
                {
                    Name = ar.ReadString(),
                    F1 = ar.ReadSingle(),
                    F2 = ar.ReadSingle()
                }));

            // Read animatable strings
            count = ar.ReadInt32();
            anim.Animatables.Clear();
            anim.Animatables.AddRange(
                RepeatFor(count, () => ar.ReadString()));
        }
        
        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var anim = data as Anim;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);

            aw.Write((int)anim.AnimEntries.Count);
            anim.AnimEntries.ForEach(x =>
            {
                aw.Write((string)x.Name);
                aw.Write((float)x.F1);
                aw.Write((float)x.F2);
            });

            aw.Write((int)anim.Animatables.Count);
            anim.Animatables.ForEach(x => aw.Write((string)x));
        }
        
        public override bool IsOfType(ISerializable data) => data is Anim;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 0;
                case 24:
                    // GH2
                    return 4;
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
                    return new[] { 0 };
                case 24:
                    // GH2
                    return new[] { 4 };
                case 25:
                    // TBRB
                    return new[] { 4 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
