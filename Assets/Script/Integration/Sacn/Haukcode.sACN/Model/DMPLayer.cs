using System;

namespace Haukcode.sACN.Model
{
    public class DMPLayer
    {
        public const byte DMP_VECTOR = 2;
        public const byte ADDRESS_TYPE_AND_DATA_TYPE = 0xA1;
        public const short FIRST_PROPERTY_ADDRESS = 0x00;
        public const short ADDRESS_INCREMENT = 1;

        public byte StartCode { get; set; }

        public short Length { get { return (short)(11 + Data.Length); } }

        public ReadOnlyMemory<byte> Data { get; set; }

        public DMPLayer(ReadOnlyMemory<byte> data, byte startCode = 0x00)
        {
            Data = data;
            StartCode = startCode;
        }

        public int WriteToBuffer(Memory<byte> buffer)
        {
            var writer = new BigEndianBinaryWriter(buffer);

            ushort flagsAndDMPLength = (ushort)(SACNDataPacket.FLAGS | (ushort)Length);

            writer.WriteUShort(flagsAndDMPLength);
            writer.WriteByte(DMP_VECTOR);
            writer.WriteByte(ADDRESS_TYPE_AND_DATA_TYPE);
            writer.WriteShort(FIRST_PROPERTY_ADDRESS);
            writer.WriteShort(ADDRESS_INCREMENT);
            writer.WriteShort((short)(Data.Length + 1));
            writer.WriteByte(StartCode);
            writer.WriteBytes(Data);

            return writer.WrittenBytes;
        }
    }
}
