using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using YARG.Audio;

namespace YARG.Serialization
{
    public class XboxImageSettings
    {
        public readonly byte bitsPerPixel;
        public readonly int format;
        public readonly int width;
        public readonly int height;
        public unsafe XboxImageSettings(byte[] data)
        {
            unsafe
            {
                bitsPerPixel = data[1];
                format = BinaryPrimitives.ReadInt32LittleEndian(new(data, 2, 4));
                width = BinaryPrimitives.ReadInt16LittleEndian(new(data, 7, 2));
                height = BinaryPrimitives.ReadInt16LittleEndian(new(data, 9, 2));
            }
        }
    }

    public static class XboxImageTextureGenerator
    {
#nullable enable
        public static unsafe XboxImageSettings? GetTexture(byte[] data, CancellationToken ct)
        {
            // Swap bytes because xbox is weird like that
            byte buf;
            for (int i = 32; i < data.Length; i += 2)
            {
                if (ct.IsCancellationRequested)
                    return null;

                buf = data[i];
                data[i] = data[i + 1];
                data[i + 1] = buf;
            }

            return new XboxImageSettings(data);
        }
    }
}