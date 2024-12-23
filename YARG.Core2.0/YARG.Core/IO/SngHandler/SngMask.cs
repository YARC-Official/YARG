using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace YARG.Core.IO
{
    /// <summary>
    /// Handles the buffer of decryption keys, while also providing easy access
    /// to SIMD vector operations through pointers and fixed array behavior.
    /// </summary>
    public unsafe struct SngMask
    {
        public const int NUM_KEYBYTES = 256;
        public const int MASKLENGTH = 16;
        public static readonly int VECTORBYTE_COUNT = Vector<byte>.Count;
        public static readonly int NUMVECTORS = NUM_KEYBYTES / VECTORBYTE_COUNT;

        public fixed byte Keys[NUM_KEYBYTES];

        public SngMask(Stream stream)
        {
            Span<byte> mask = stackalloc byte[MASKLENGTH];
            stream.Read(mask);

            for (int i = 0; i < NUM_KEYBYTES;)
            {
                for (int j = 0; j < MASKLENGTH; i++, j++)
                {
                    Keys[i] = (byte) (mask[j] ^ i);
                }
            }
        }
    }
}
