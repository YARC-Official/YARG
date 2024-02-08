using System;

namespace Haukcode.sACN.Model
{
    public class SACNPacket
    {
        public const ushort FLAGS = (0x7 << 12);
        public const ushort FIRST_FOUR_BITS_MASK = 0b1111_0000_0000_0000;
        public const ushort LAST_TWELVE_BITS_MASK = 0b0000_1111_1111_1111;
        public const int MAX_PACKET_SIZE = 638;

        public RootLayer RootLayer { get; set; }

        public FramingLayer FramingLayer => RootLayer.FramingLayer;

        public Guid UUID { get { return RootLayer.UUID; } set { RootLayer.UUID = value; } }

        public byte SequenceId { get { return FramingLayer.SequenceId; } set { FramingLayer.SequenceId = value; } }

        public int Length => RootLayer.Length;

        public SACNPacket(RootLayer rootLayer)
        {
            RootLayer = rootLayer;
        }

        public int WriteToBuffer(Memory<byte> buffer)
        {
            return RootLayer.WriteToBuffer(buffer);
        }
    }
}
