using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class FontSerializer : AbstractSerializer
    {
        public FontSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }
        
        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var font = data as Font;
            int version = ReadMagic(ar, data);

            font.Material = ar.ReadString();
            font.CharacterWidth = ar.ReadSingle();
            font.CharacterHeight = ar.ReadSingle();

            float unknown = ar.ReadSingle();
            if (unknown != 26.0f)
                throw new Exception($"Expected 26, got {unknown:F2}");

            unknown = ar.ReadSingle();
            if (unknown != 0.0f)
                throw new Exception($"Expected 0, got {unknown:F2}");

            font.Chracters = ar.ReadString().ToCharArray();

            if (!ar.ReadBoolean())
                throw new Exception("This should always be true");

            int count = ar.ReadInt32();
            font.FontEntries.Clear();
            font.FontEntries.AddRange(RepeatFor(count, () => new FontEntry()
            {
                Unknown = ar.ReadInt32(),
                UnknownF = ar.ReadSingle()
            }));
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            throw new NotImplementedException();
        }

        public override bool IsOfType(ISerializable data) => data is Font;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 7;
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
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
