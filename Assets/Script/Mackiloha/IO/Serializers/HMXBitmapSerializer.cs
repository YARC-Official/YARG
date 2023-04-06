using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.IO.Serializers
{
    public class HMXBitmapSerializer : AbstractSerializer
    {
        public HMXBitmapSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var bitmap = data as HMXBitmap;

            if (ar.ReadByte() != 0x01)
                throw new NotSupportedException($"HMXBitmapReader: Expected 0x01 at offset 0");

            bitmap.Bpp = ar.ReadByte();
            bitmap.Encoding = ar.ReadInt32();
            bitmap.MipMaps = ar.ReadByte();

            bitmap.Width = ar.ReadUInt16();
            bitmap.Height = ar.ReadUInt16();
            bitmap.BPL = ar.ReadUInt16();

            ar.BaseStream.Position += 19; // Skips zeros
            bitmap.RawData = ar.ReadBytes(CalculateTextureByteSize(bitmap.Encoding, bitmap.Width, bitmap.Height, bitmap.Bpp, bitmap.MipMaps));
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var bitmap = data as HMXBitmap;

            aw.Write((byte)0x01);

            aw.Write((byte)bitmap.Bpp);
            aw.Write((int)bitmap.Encoding);
            aw.Write((byte)bitmap.MipMaps);

            aw.Write((short)bitmap.Width);
            aw.Write((short)bitmap.Height);
            aw.Write((short)bitmap.BPL);

            aw.Write(new byte[19]);

            byte[] bytes = new byte[CalculateTextureByteSize(bitmap.Encoding, bitmap.Width, bitmap.Height, bitmap.Bpp, bitmap.MipMaps)];
            Array.Copy(bitmap.RawData, bytes, bytes.Length);
            aw.Write(bytes);
        }

        private int CalculateTextureByteSize(int encoding, int w, int h, int bpp, int mips)
        {
            int bytes = 0;

            // Adds color palette if applicable
            switch (encoding)
            {
                case 3:
                case 8:
                    // Only encoding 8 is bitmap on xbox?
                    if (encoding == 8 && MiloSerializer.Info.Platform != Platform.XBOX)
                        break;

                    // Each color is 32 bits 
                    bytes += (bpp == 4 || bpp == 8) ? 1 << (bpp + 2) : 0;
                    break;
            }

            while (mips >= 0)
            {
                bytes += (w * h * bpp) / 8;
                w >>= 1;
                h >>= 1;
                mips -= 1;
            }

            return bytes;
        }

        public override bool IsOfType(ISerializable data) => data is HMXBitmap;

        public override int Magic() => 1;

        internal override int[] ValidMagics()
        {
            return new[] { 1 };
        }
    }
}
