using System;
using System.Runtime.InteropServices;

namespace YARG.Core.Song
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct PartValues
    {
        public static readonly PartValues Default = new()
        {
            SubTracks = 0,
            Difficulties = DifficultyMask.None,
            Intensity = -1
        };

        [FieldOffset(0)] public byte SubTracks;
        [FieldOffset(0)] public DifficultyMask Difficulties;

        [FieldOffset(1)] public sbyte Intensity;

        public readonly bool this[int subTrack]
        {
            get
            {
                const int BITS_IN_BYTE = 8;
                if (subTrack >= BITS_IN_BYTE)
                {
                    throw new Exception("Subtrack index out of range");
                }
                return ((byte) (1 << subTrack) & SubTracks) > 0;
            }
        }

        public readonly bool this[Difficulty difficulty] => this[(int) difficulty];

        public void SetSubtrack(int subTrack)
        {
            SubTracks |= (byte) (1 << subTrack);
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            Difficulties |= (DifficultyMask) (1 << (int)difficulty);
        }

        public readonly bool WasParsed() { return SubTracks > 0; }
    }
}
