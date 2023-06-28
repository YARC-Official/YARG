using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using YARG.Audio;

namespace YARG.Serialization
{
    public static class XboxImageTextureGenerator
    {
        public static async UniTask<Texture2D> GetTexture(byte[] xboxImageBytes, CancellationToken ct)
        {
            var ms = new MemoryStream(xboxImageBytes);

            // Parse header and get DXT blocks
            byte[] header = ms.ReadBytes(32);
            byte BitsPerPixel = header[1];
            int Format = BitConverter.ToInt32(header, 2);
            short Width = BitConverter.ToInt16(header, 7);
            short Height = BitConverter.ToInt16(header, 9);
            bool isDXT1 = ((BitsPerPixel == 0x04) && (Format == 0x08));
            ms.Seek(32, SeekOrigin.Begin);
            byte[] DXTBlocks = ms.ReadBytes((int) (ms.Length - 32));

            ct.ThrowIfCancellationRequested();

            // Swap bytes because xbox is weird like that
            for (int i = 0; i < DXTBlocks.Length / 2; i++)
            {
                (DXTBlocks[i * 2], DXTBlocks[i * 2 + 1]) = (DXTBlocks[i * 2 + 1], DXTBlocks[i * 2]);
            }

            ct.ThrowIfCancellationRequested();

            // apply DXT1 OR DXT5 formatted bytes to a Texture2D
            var tex = new Texture2D(Width, Height,
                (isDXT1) ? GraphicsFormat.RGBA_DXT1_SRGB : GraphicsFormat.RGBA_DXT5_SRGB, TextureCreationFlags.None);
            tex.LoadRawTextureData(DXTBlocks);
            tex.Apply();

            return tex;
        }
    }
}