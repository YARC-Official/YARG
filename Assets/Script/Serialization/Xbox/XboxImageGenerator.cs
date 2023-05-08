using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace YARG.Serialization {
    public static class XboxImageTextureGenerator {
        public static Texture2D GetTexture(byte[] xboxImageBytes){            
            var ms = new MemoryStream(xboxImageBytes);

            // Parse header
            byte[] header = ms.ReadBytes(32);
            byte BitsPerPixel = header[1];
            int Format = BitConverter.ToInt32(header, 2);
            short Width = BitConverter.ToInt16(header, 7);
            short Height = BitConverter.ToInt16(header, 9);
            byte[] DXTBlocks;

            // Parse DXT-compressed blocks, depending on format
            if ((BitsPerPixel == 0x04) && (Format == 0x08)) {
                // If DXT-1 format already, read the bytes straight up
                ms.Seek(32, SeekOrigin.Begin);
                DXTBlocks = ms.ReadBytes((int) (ms.Length - 32));
            } else {
                // If DXT-3 format, we have to omit the alpha bytes
                List<byte> extractedDXT3 = new List<byte>();
                ms.ReadBytes(8); //skip the first 8 alpha bytes
                for (int i = 8; i < (ms.Length - 32) / 2; i += 8) {
                    extractedDXT3.AddRange(ms.ReadBytes(8)); // We want to read these 8 bytes
                    ms.ReadBytes(8); // and skip these 8 bytes
                }
                DXTBlocks = extractedDXT3.ToArray();
            }

            // Swap bytes because xbox is weird like that
            for(int i = 0; i < DXTBlocks.Length / 2; i++)
                (DXTBlocks[i * 2], DXTBlocks[i * 2 + 1]) = (DXTBlocks[i * 2 + 1], DXTBlocks[i * 2]);

            // apply DXT1 formatted bytes to a Texture2D
            var tex = new Texture2D(Width, Height, GraphicsFormat.RGBA_DXT1_SRGB, TextureCreationFlags.None);
            tex.LoadRawTextureData(DXTBlocks);
            tex.Apply();

            return tex;
        }
    }
}