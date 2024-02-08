using System;

namespace Haukcode.sACN.Model
{
    public class SyncFramingLayer : FramingLayer
    {
        public override ushort Length { get { return 11; } }

        public ushort SyncAddress { get; set; }

        public override int RootVector => RootLayer.VECTOR_ROOT_E131_EXTENDED;

        public SyncFramingLayer(ushort syncAddress, byte sequenceID)
            : base(sequenceID)
        {
            SyncAddress = syncAddress;
        }

        public SyncFramingLayer()
        {
        }

        public override int WriteToBuffer(Memory<byte> buffer)
        {
            var writer = new BigEndianBinaryWriter(buffer);

            ushort flagsAndFramingLength = (ushort)(SACNPacket.FLAGS | Length);
            writer.WriteUShort(flagsAndFramingLength);
            writer.WriteInt32(VECTOR_E131_EXTENDED_SYNCHRONIZATION);
            writer.WriteByte(SequenceId);
            writer.WriteUShort(SyncAddress);
            writer.WriteUShort((ushort)0);

            return writer.WrittenBytes;
        }
    }
}
