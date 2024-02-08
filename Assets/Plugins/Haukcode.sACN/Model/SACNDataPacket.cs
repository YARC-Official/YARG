using System;

namespace Haukcode.sACN.Model
{
    public class SACNDataPacket : SACNPacket
    {
        public DataFramingLayer DataFramingLayer => (DataFramingLayer)RootLayer.FramingLayer;

        public string SourceName { get { return DataFramingLayer.SourceName; } set { DataFramingLayer.SourceName = value; } }

        public byte[] DMXData => DataFramingLayer.DMPLayer.Data.ToArray();

        public ushort UniverseId { get { return DataFramingLayer.UniverseId; } set { DataFramingLayer.UniverseId = value; } }

        public SACNDataPacket(ushort universeId, string sourceName, Guid uuid, byte sequenceId, ReadOnlyMemory<byte> data, byte priority, ushort syncAddress = 0, byte startCode = 0)
            : base(RootLayer.CreateRootLayerData(uuid, sourceName, universeId, sequenceId, data, priority, syncAddress, startCode))
        {
        }

        public SACNDataPacket(RootLayer rootLayer)
            : base(rootLayer)
        {
        }
    }
}
