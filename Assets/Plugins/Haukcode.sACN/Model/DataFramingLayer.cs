using System;


namespace Haukcode.sACN.Model
{
    public class DataFramingLayer : FramingLayer
    {
        public const int SourceNameLength = 64;

        public DMPLayer DMPLayer { get; set; }

        public override ushort Length { get { return (ushort)(13 + SourceNameLength + DMPLayer.Length); } }

        public string SourceName { get; set; }

        public ushort UniverseId { get; set; }

        public byte Priority { get; set; }

        public ushort SyncAddress { get; set; }

        public FramingOptions Options { get; set; }

        public override int RootVector => RootLayer.VECTOR_ROOT_E131_DATA;

        public DataFramingLayer(string sourceName, ushort universeId, byte sequenceId, ReadOnlyMemory<byte> data, byte priority, ushort syncAddress = 0, byte startCode = 0)
            : base(sequenceId)
        {
            SourceName = sourceName;
            UniverseId = universeId;
            DMPLayer = new DMPLayer(data, startCode);
            Priority = priority;
            SyncAddress = syncAddress;
            Options = new FramingOptions();
        }

        public DataFramingLayer()
        {
        }

        public override int WriteToBuffer(Memory<byte> buffer)
        {
            var writer = new BigEndianBinaryWriter(buffer);

            ushort flagsAndFramingLength = (ushort)(SACNPacket.FLAGS | Length);
            writer.WriteUShort(flagsAndFramingLength);
            writer.WriteInt32(VECTOR_E131_DATA_PACKET);
            writer.WriteString(SourceName, 64);
            writer.WriteByte(Priority);
            writer.WriteUShort(SyncAddress);
            writer.WriteByte(SequenceId);
            writer.WriteByte(Options.ToByte());
            writer.WriteUShort(UniverseId);

            return writer.WrittenBytes + DMPLayer.WriteToBuffer(writer.Memory);
        }
    }
}
