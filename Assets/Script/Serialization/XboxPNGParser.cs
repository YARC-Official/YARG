using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --------------------------------------------------------------------------------
// original source: https://github.com/Benjamin-Dobell/s3tc-dxt-decompression
// S3TC DXT1 / DXT5 Texture Decompression Routines
// Original Author: Benjamin Dobell - http://www.glassechidna.com.au
// Code was originally written in C++
// However, for the purposes of YARG, the relevant decompression code has been ported to C#.
// --------------------------------------------------------------------------------

namespace YARG.Serialization {
    public static class XboxPNGParser {
        // PackRGBA(): Helper method that packs RGBA channels into a single 4 byte pixel.
        private static uint PackRGBA(byte r, byte g, byte b, byte a) { return (uint)((r << 24) | (g << 16) | (b << 8) | a); }

        // DecompressBlockDXT1(): Decompresses one block of a DXT1 texture and stores the resulting pixels at the appropriate offset in 'image'.
        private static void DecompressBlockDXT1(uint x, uint y, uint width, byte[] blockStorage, uint[] image){
            ushort color0 = BitConverter.ToUInt16(blockStorage, 0);
            ushort color1 = BitConverter.ToUInt16(blockStorage, 2);
            uint temp;

            temp = (uint)(color0 >> 11) * 255 + 16;
            byte r0 = (byte)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            byte g0 = (byte)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            byte b0 = (byte)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            byte r1 = (byte)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            byte g1 = (byte)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            byte b1 = (byte)((temp / 32 + temp) / 32);

            uint code = BitConverter.ToUInt32(blockStorage, 4);

            for(int j = 0; j < 4; j++){
                for(int i = 0; i < 4; i++){
                    uint finalColor = 0;
                    byte positionCode = (byte)((code >> 2 * (4 * j + i)) & 0x03);

                    switch(positionCode){
                        case 0:
                            finalColor = PackRGBA(r0, g0, b0, 255);
                            break;
                        case 1:
                            finalColor = PackRGBA(r1, g1, b1, 255);
                            break;
                        case 2:
                            if(color0 > color1) finalColor = PackRGBA((byte)((2*r0+r1)/3), (byte)((2*g0+g1)/3), (byte)((2*b0+b1)/3), 255);
                            else finalColor = PackRGBA((byte)((r0+r1)/2), (byte)((g0+g1)/2), (byte)((b0+b1)/2), 255);
                            break;
                        case 3:
                            if(color0 > color1) finalColor = PackRGBA((byte)((r0+2*r1)/3), (byte)((g0+2*g1)/3), (byte)((b0+2*b1)/3), 255);
                            else finalColor = PackRGBA(0, 0, 0, 255);
                            break;
                    }
                    
                    if (x + i < width)
                        image[(y + j)*width + (x + i)] = finalColor;
                }
            }
        }
        // BlockDecompressImageDXT1(): Decompresses all the blocks of a DXT1 compressed texture and stores the resulting pixels in 'image'.
        public static void BlockDecompressImageDXT1(uint width, uint height, byte[] blockStorage, uint[] image){
            uint blockCountX = (width + 3) / 4;
            uint blockCountY = (height + 3) / 4;
            // uint blockWidth = (width < 4) ? width : 4;
            // uint blockHeight = (height < 4) ? height : 4;

            for(uint j = 0; j < blockCountY; j++){
                for(uint i = 0; i < blockCountX; i++){
                    DecompressBlockDXT1(i*4, j*4, width, blockStorage, image);
                    blockStorage = blockStorage.Skip(8).ToArray();
                }
            }
        }
    }
}
