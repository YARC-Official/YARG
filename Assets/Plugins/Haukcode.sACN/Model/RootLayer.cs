using System;

namespace Haukcode.sACN.Model
{
    public class RootLayer
    {
        public const short PREAMBLE_LENGTH = 0x0010;
        public const short POSTAMBLE_LENGTH = 0x0000;
        public static readonly byte[] PACKET_IDENTIFIER = new byte[] {
            0x41, 0x53, 0x43, 0x2d, 0x45,
            0x31, 0x2e, 0x31, 0x37, 0x00,
            0x00, 0x00};
        public const int VECTOR_ROOT_E131_DATA = 0x00000004;
        public const int VECTOR_ROOT_E131_EXTENDED = 0x00000008;

        public FramingLayer FramingLayer { get; set; }

        public short Length { get { return (short)(38 + FramingLayer.Length); } }

        public Guid UUID { get; set; }

        public RootLayer()
        {
        }

        public static RootLayer CreateRootLayerData(Guid uuid, string sourceName, ushort universeID, byte sequenceID, ReadOnlyMemory<byte> data, byte priority, ushort syncAddress, byte startCode = 0)
        {
            return new RootLayer
            {
                UUID = uuid,
                FramingLayer = new DataFramingLayer(sourceName, universeID, sequenceID, data, priority, syncAddress, startCode)
            };
        }

        public static RootLayer CreateRootLayerSync(Guid uuid, byte sequenceID, ushort syncAddress)
        {
            return new RootLayer
            {
                UUID = uuid,
                FramingLayer = new SyncFramingLayer(syncAddress, sequenceID)
            };
        }

        public int WriteToBuffer(Memory<byte> buffer)
        {
            var writer = new BigEndianBinaryWriter(buffer);

            writer.WriteShort(PREAMBLE_LENGTH);
            writer.WriteShort(POSTAMBLE_LENGTH);
            writer.WriteBytes(PACKET_IDENTIFIER);
            ushort flagsAndRootLength = (ushort)(SACNPacket.FLAGS | (ushort)(Length - 16));
            writer.WriteUShort(flagsAndRootLength);
            writer.WriteInt32(FramingLayer.RootVector);
            writer.WriteGuid(UUID);

            return writer.WrittenBytes + FramingLayer.WriteToBuffer(writer.Memory);
        }
    }
}
