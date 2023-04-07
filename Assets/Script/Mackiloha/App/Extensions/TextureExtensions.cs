using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mackiloha.IO;
using Mackiloha.Render;

namespace Mackiloha.App.Extensions
{
    public static class TextureExtensions
    {
        private enum DxEncoding : int
        {
            DXGI_FORMAT_BC1_UNORM =  8, // DXT1
            DXGI_FORMAT_BC3_UNORM = 24, // DXT5
            DXGI_FORMAT_BC5_UNORM = 32  // ATI2
        }

        public static byte[] ToRGBA(this HMXBitmap bitmap, SystemInfo info)
        {
            switch (bitmap.Encoding)
            {
                case 2: // Phase
                case 3:
                    var image = DecodeBitmap(
                        bitmap.RawData,
                        bitmap.Width,
                        bitmap.Height,
                        bitmap.MipMaps,
                        bitmap.Bpp,
                        (info.Platform == Platform.PC) ? 16 : 32);

                    // Converts if needed
                    if (info.Platform == Platform.XBOX) // BGRa->RGBa TODO: Re-verify
                        SwapRBColors(image);
                    else if (info.Platform == Platform.X360) // aRGB -> RGBa
                        ShiftChannelsLeft(image);

                    // Update alpha channel
                    if (info.Platform == Platform.PS2 && bitmap.Bpp != 24)
                        UpdateAlphaTo8Bit(image);

                    return image;
                case 8: // DXT1 or Bitmap
                case 24: // DXT5
                case 32: // ATI2
                    if (bitmap.Encoding == 8 && info.Platform == Platform.XBOX)
                    {
                        var image2 = DecodeBitmap(bitmap.RawData, bitmap.Width, bitmap.Height, bitmap.MipMaps, bitmap.Bpp);
                        SwapRBColors(image2);
                        return image2;
                    }

                    var tempData = new byte[bitmap.RawData.Length];
                    Array.Copy(bitmap.RawData, tempData, tempData.Length);

                    if (info.Platform == Platform.X360)
                        SwapBytes(tempData);

                    return DecodeDxImage(tempData, bitmap.Width, bitmap.Height, bitmap.MipMaps, (DxEncoding)bitmap.Encoding);
                default:
                    return null;
            }
        }

        private static void SwapRBColors(byte[] image)
        {
            byte temp;

            for (int i = 0; i < image.Length; i += 16)
            {
                // Pixel 1
                temp          = image[i     ];
                image[i     ] = image[i +  2];
                image[i +  2] = temp;

                // Pixel 2
                temp          = image[i +  4];
                image[i +  4] = image[i +  6];
                image[i +  6] = temp;

                // Pixel 3
                temp          = image[i +  8];
                image[i +  8] = image[i + 10];
                image[i + 10] = temp;

                // Pixel 4
                temp         =  image[i + 12];
                image[i + 12] = image[i + 14];
                image[i + 14] = temp;
            }
        }

        private static void ShiftChannelsLeft(byte[] image)
        {
            byte temp;

            for (int i = 0; i < image.Length; i += 16)
            {
                // Pixel 1
                temp          = image[i     ];
                image[i     ] = image[i +  1];
                image[i +  1] = image[i +  2];
                image[i +  2] = image[i +  3];
                image[i +  3] = temp;

                // Pixel 2
                temp          = image[i +  4];
                image[i +  4] = image[i +  5];
                image[i +  5] = image[i +  6];
                image[i +  6] = image[i +  7];
                image[i +  7] = temp;

                // Pixel 3
                temp          = image[i +  8];
                image[i +  8] = image[i +  9];
                image[i +  9] = image[i + 10];
                image[i + 10] = image[i + 11];
                image[i + 11] = temp;

                // Pixel 4
                temp          = image[i + 12];
                image[i + 12] = image[i + 13];
                image[i + 13] = image[i + 14];
                image[i + 14] = image[i + 15];
                image[i + 15] = temp;
            }
        }

        private static void SwapBytes(byte[] image)
        {
            byte temp;

            for (int i = 0; i < image.Length; i += 2)
            {
                temp = image[i];
                image[i    ] = image[i + 1];
                image[i + 1] = temp;
            }
        }

        private static void UpdateAlphaTo8Bit(byte[] image)
        {
            // Updates alpha channels (7-bit -> 8-bit)
            byte al;
            for (int p = 3; p < image.Length; p += 4)
            {
                al = image[p];
                image[p] = ((al & 0x80) != 0) ? (byte)0xFF : (byte)(al << 1);
            }
        }

        private static byte[] DecodeBitmap(byte[] raw, int width, int height, int mips, int bpp, int colorSize = 32)
        {
            byte[] image = new byte[width * height * 4]; // 32 bpp

            // TODO: Take into account mip maps
            if (bpp == 32)
            {
                Array.Copy(raw, image, image.Length);
                return image;
            }
            else if (bpp == 24)
            {
                int r = 0;
                for (int i = 0; i < image.Length; i += 16)
                {
                    // Pixel 1
                    image[i    ]  = raw[r     ];
                    image[i + 1]  = raw[r +  1];
                    image[i + 2]  = raw[r +  2];
                    image[i + 3]  = 0xFF;
                    // Pixel 2
                    image[i + 4]  = raw[r +  3];
                    image[i + 5]  = raw[r +  4];
                    image[i + 6]  = raw[r +  5];
                    image[i + 7]  = 0xFF;
                    // Pixel 3
                    image[i +  8] = raw[r +  6];
                    image[i +  9] = raw[r +  7];
                    image[i + 10] = raw[r +  8];
                    image[i + 11] = 0xFF;
                    // Pixel 4
                    image[i + 12] = raw[r +  9];
                    image[i + 13] = raw[r + 10];
                    image[i + 14] = raw[r + 11];
                    image[i + 15] = 0xFF;
                    r += 12;
                }
                return image;
            }
            else if (bpp == 16)
            {
                int r = 0;
                for (int i = 0; i < image.Length; i += 16)
                {
                    // TODO: Figure out if big endian is encoded differently

                    // Pixel 1
                    image[i    ]  = _4BitsTo8Bits(raw[r  + 1], 0xF0);
                    image[i + 1]  = _4BitsTo8Bits(raw[r  + 1], 0x0F);
                    image[i + 2]  = _4BitsTo8Bits(raw[r     ], 0xF0);
                    image[i + 3]  = _4BitsTo8Bits(raw[r     ], 0x0F);
                    // Pixel 2
                    image[i + 4]  = _4BitsTo8Bits(raw[r  + 3], 0xF0);
                    image[i + 5]  = _4BitsTo8Bits(raw[r  + 3], 0x0F);
                    image[i + 6]  = _4BitsTo8Bits(raw[r  + 2], 0xF0);
                    image[i + 7]  = _4BitsTo8Bits(raw[r  + 2], 0x0F);
                    // Pixel 3
                    image[i +  8] = _4BitsTo8Bits(raw[r  + 5], 0xF0);
                    image[i +  9] = _4BitsTo8Bits(raw[r  + 5], 0x0F);
                    image[i + 10] = _4BitsTo8Bits(raw[r  + 4], 0xF0);
                    image[i + 11] = _4BitsTo8Bits(raw[r  + 4], 0x0F);
                    // Pixel 4
                    image[i + 12] = _4BitsTo8Bits(raw[r  + 7], 0xF0);
                    image[i + 13] = _4BitsTo8Bits(raw[r  + 7], 0x0F);
                    image[i + 14] = _4BitsTo8Bits(raw[r  + 6], 0xF0);
                    image[i + 15] = _4BitsTo8Bits(raw[r  + 6], 0x0F);
                    r += 8;
                }

                return image;
            }

            byte[] palette = new byte[1 << (bpp + 2)];

            if (colorSize == 32)
            {
                Array.Copy(raw, palette, palette.Length);
            }
            else if (colorSize == 16)
            {
                int r = 0;
                for (int i = 0; i < palette.Length; i += 16)
                {
                    // TODO: Figure out if big endian is encoded differently

                    // Pixel 1
                    palette[i    ]  = _4BitsTo8Bits(raw[r  + 1], 0xF0);
                    palette[i + 1]  = _4BitsTo8Bits(raw[r  + 1], 0x0F);
                    palette[i + 2]  = _4BitsTo8Bits(raw[r     ], 0xF0);
                    palette[i + 3]  = _4BitsTo8Bits(raw[r     ], 0x0F);
                    // Pixel 2
                    palette[i + 4]  = _4BitsTo8Bits(raw[r  + 3], 0xF0);
                    palette[i + 5]  = _4BitsTo8Bits(raw[r  + 3], 0x0F);
                    palette[i + 6]  = _4BitsTo8Bits(raw[r  + 2], 0xF0);
                    palette[i + 7]  = _4BitsTo8Bits(raw[r  + 2], 0x0F);
                    // Pixel 3
                    palette[i +  8] = _4BitsTo8Bits(raw[r  + 5], 0xF0);
                    palette[i +  9] = _4BitsTo8Bits(raw[r  + 5], 0x0F);
                    palette[i + 10] = _4BitsTo8Bits(raw[r  + 4], 0xF0);
                    palette[i + 11] = _4BitsTo8Bits(raw[r  + 4], 0x0F);
                    // Pixel 4
                    palette[i + 12] = _4BitsTo8Bits(raw[r  + 7], 0xF0);
                    palette[i + 13] = _4BitsTo8Bits(raw[r  + 7], 0x0F);
                    palette[i + 14] = _4BitsTo8Bits(raw[r  + 6], 0xF0);
                    palette[i + 15] = _4BitsTo8Bits(raw[r  + 6], 0x0F);
                    r += 8;
                }
            }
            
            var o = (palette.Length / 32) * colorSize; // Pixel start offset
            
            if (bpp == 4)
            {
                int r = 0, p1, p2, p3, p4;
                for (int i = 0; i < image.Length; i += 16)
                {
                    // Palette offsets
                    p1 = (raw[ o + r    ] & 0x0F) << 2;
                    p2 = (raw[ o + r    ] & 0xF0) >> 2;
                    p3 = (raw[ o + r + 1] & 0x0F) << 2;
                    p4 = (raw[ o + r + 1] & 0xF0) >> 2;
                    // Pixel 1
                    image[i     ] = palette[p1    ];
                    image[i +  1] = palette[p1 + 1];
                    image[i +  2] = palette[p1 + 2];
                    image[i +  3] = palette[p1 + 3];
                    // Pixel 2
                    image[i +  4] = palette[p2    ];
                    image[i +  5] = palette[p2 + 1];
                    image[i +  6] = palette[p2 + 2];
                    image[i +  7] = palette[p2 + 3];
                    // Pixel 3
                    image[i +  8] = palette[p3    ];
                    image[i +  9] = palette[p3 + 1];
                    image[i + 10] = palette[p3 + 2];
                    image[i + 11] = palette[p3 + 3];
                    // Pixel 4
                    image[i + 12] = palette[p4    ];
                    image[i + 13] = palette[p4 + 1];
                    image[i + 14] = palette[p4 + 2];
                    image[i + 15] = palette[p4 + 3];
                    r += 2;
                }
            }
            else if (bpp == 8)
            {
                int r = 0, p1, p2, p3, p4;
                for (int i = 0; i < image.Length; i += 16)
                {
                    // Palette offsets
                    // Swaps bits 3 and 4 with eachother
                    // Ex: 0110 1011 -> 0111 0011
                    p1 = ((0xE7 & raw[o + r    ]) | ((0x08 & raw[o + r    ]) << 1) | ((0x10 & raw[o + r    ]) >> 1)) << 2;
                    p2 = ((0xE7 & raw[o + r + 1]) | ((0x08 & raw[o + r + 1]) << 1) | ((0x10 & raw[o + r + 1]) >> 1)) << 2;
                    p3 = ((0xE7 & raw[o + r + 2]) | ((0x08 & raw[o + r + 2]) << 1) | ((0x10 & raw[o + r + 2]) >> 1)) << 2;
                    p4 = ((0xE7 & raw[o + r + 3]) | ((0x08 & raw[o + r + 3]) << 1) | ((0x10 & raw[o + r + 3]) >> 1)) << 2;
                    // Pixel 1
                    image[i     ] = palette[p1    ];
                    image[i +  1] = palette[p1 + 1];
                    image[i +  2] = palette[p1 + 2];
                    image[i +  3] = palette[p1 + 3];
                    // Pixel 2
                    image[i +  4] = palette[p2    ];
                    image[i +  5] = palette[p2 + 1];
                    image[i +  6] = palette[p2 + 2];
                    image[i +  7] = palette[p2 + 3];
                    // Pixel 3
                    image[i +  8] = palette[p3    ];
                    image[i +  9] = palette[p3 + 1];
                    image[i + 10] = palette[p3 + 2];
                    image[i + 11] = palette[p3 + 3];
                    // Pixel 4
                    image[i + 12] = palette[p4    ];
                    image[i + 13] = palette[p4 + 1];
                    image[i + 14] = palette[p4 + 2];
                    image[i + 15] = palette[p4 + 3];
                    r += 4;
                }
            }
            
            return image;
        }

        private static byte[] DecodeDxImage(byte[] raw, int width, int height, int mips, DxEncoding encoding)
        {
            byte[] image = new byte[width * height * 4]; // 32 bpp

            // TODO: Make parameter instead
            int blockX = width >> 2;
            int blockY = height >> 2;
            int blockSize = 8; // (16 pixels * 4bpp) / 8
            
            int[] colors = new int[4];
            int[] pixelIndices = new int[16];
            int[] pixels = new int[16];

            byte[] colorRgba = new byte[16];

            (byte R, byte G, byte B, byte A)[] colors2 = new (byte, byte, byte, byte)[4];
            
            int i = 0, x, y;
            ushort packed0, packed1;

            // For DXT5
            byte[] alphas = new byte[8];
            byte[] alphaPixels = new byte[16];

            for (int by = 0; by < blockY; by++)
            {
                for (int bx = 0; bx < blockX; bx++)
                {
                    x = bx << 2;
                    y = by << 2;

                    if (encoding == DxEncoding.DXGI_FORMAT_BC5_UNORM)
                    {
                        var reds = UnpackIndexedInterpolatedColors(raw , i);
                        var greens = UnpackIndexedInterpolatedColors(raw, i + 8);
                        var nomalColors = new byte[64];

                        for (int c = 0; c < reds.Length; c++)
                        {
                            nomalColors[(c << 2)    ] =   reds[c];
                            nomalColors[(c << 2) + 1] = greens[c];
                            nomalColors[(c << 2) + 2] =      0x00;
                            nomalColors[(c << 2) + 3] =      0xFF;
                        }

                        Array.Copy(nomalColors, 0, image, LinearOffset(x    , y    , width), 4);
                        Array.Copy(nomalColors, 4, image, LinearOffset(x + 1, y    , width), 4);
                        Array.Copy(nomalColors, 8, image, LinearOffset(x + 2, y    , width), 4);
                        Array.Copy(nomalColors, 12, image, LinearOffset(x + 3, y    , width), 4);

                        Array.Copy(nomalColors, 16, image, LinearOffset(x    , y + 1, width), 4);
                        Array.Copy(nomalColors, 20, image, LinearOffset(x + 1, y + 1, width), 4);
                        Array.Copy(nomalColors, 24, image, LinearOffset(x + 2, y + 1, width), 4);
                        Array.Copy(nomalColors, 28, image, LinearOffset(x + 3, y + 1, width), 4);

                        Array.Copy(nomalColors, 32, image, LinearOffset(x    , y + 2, width), 4);
                        Array.Copy(nomalColors, 36, image, LinearOffset(x + 1, y + 2, width), 4);
                        Array.Copy(nomalColors, 40, image, LinearOffset(x + 2, y + 2, width), 4);
                        Array.Copy(nomalColors, 44, image, LinearOffset(x + 3, y + 2, width), 4);

                        Array.Copy(nomalColors, 48, image, LinearOffset(x    , y + 3, width), 4);
                        Array.Copy(nomalColors, 52, image, LinearOffset(x + 1, y + 3, width), 4);
                        Array.Copy(nomalColors, 56, image, LinearOffset(x + 2, y + 3, width), 4);
                        Array.Copy(nomalColors, 60, image, LinearOffset(x + 3, y + 3, width), 4);

                        i += blockSize << 1;
                        continue;
                    }

                    if (encoding == DxEncoding.DXGI_FORMAT_BC3_UNORM)
                    {
                        alphas[0] = raw[i    ];
                        alphas[1] = raw[i + 1];

                        if (alphas[0] > alphas[1])
                        {
                            // 6-bit interpolated alpha values
                            alphas[2] = (byte)(((6.0 / 7.0) * alphas[0]) + ((1.0 / 7.0) * alphas[1]));
                            alphas[3] = (byte)(((5.0 / 7.0) * alphas[0]) + ((2.0 / 7.0) * alphas[1]));
                            alphas[4] = (byte)(((4.0 / 7.0) * alphas[0]) + ((3.0 / 7.0) * alphas[1]));
                            alphas[5] = (byte)(((3.0 / 7.0) * alphas[0]) + ((4.0 / 7.0) * alphas[1]));
                            alphas[6] = (byte)(((2.0 / 7.0) * alphas[0]) + ((5.0 / 7.0) * alphas[1]));
                            alphas[7] = (byte)(((1.0 / 7.0) * alphas[0]) + ((6.0 / 7.0) * alphas[1]));
                        }
                        else
                        {
                            // 4-bit interpolated alpha values
                            alphas[2] = (byte)(((4.0 / 5.0) * alphas[0]) + ((1.0 / 5.0) * alphas[1]));
                            alphas[3] = (byte)(((3.0 / 5.0) * alphas[0]) + ((2.0 / 5.0) * alphas[1]));
                            alphas[4] = (byte)(((2.0 / 5.0) * alphas[0]) + ((3.0 / 5.0) * alphas[1]));
                            alphas[5] = (byte)(((1.0 / 5.0) * alphas[0]) + ((4.0 / 5.0) * alphas[1]));
                            alphas[6] = 0x00;
                            alphas[7] = 0xFF;
                        }

                        int packedAlpha0 = (raw[i + 4] << 16) | (raw[i + 3] << 8) | (raw[i + 2]);
                        int packedAlpha1 = (raw[i + 7] << 16) | (raw[i + 6] << 8) | (raw[i + 5]);

                        var alphaInd = Unpack24Bitndicies(packedAlpha0);
                        for (int id = 0; id < alphaInd.Length; id++)
                            alphaPixels[id] = alphas[alphaInd[id]];

                        alphaInd = Unpack24Bitndicies(packedAlpha1);
                        for (int id = 0; id < alphaInd.Length; id++)
                            alphaPixels[id + 8] = alphas[alphaInd[id]];

                        i += blockSize;
                    }

                    packed0 = (ushort)(raw[i    ] | raw[i + 1] << 8);
                    packed1 = (ushort)(raw[i + 2] | raw[i + 3] << 8);


                    colors[0] = ReadRGBAFromRGB565(packed0);
                    colors[1] = ReadRGBAFromRGB565(packed1);

                    colors2[0] = RGBAFromRGB565(packed0);
                    colors2[1] = RGBAFromRGB565(packed1);

                    if (!(encoding == DxEncoding.DXGI_FORMAT_BC3_UNORM) && packed0 <= packed1)
                    {
                        colors[2] = AddRGBAColors(MultiplyRGBAColors(colors[0], 0.5f), MultiplyRGBAColors(colors[1], 0.5f));
                        colors[3] = 0;

                        colors2[2] = ((byte)((colors2[0].R + colors2[1].R) / 2), (byte)((colors2[0].G + colors2[1].G) / 2), (byte)((colors2[0].B + colors2[1].B) / 2), 0xFF);

                        //colors2[2] = ((byte)(colors2[0].R * 0.5 + colors2[1].R * 0.5), (byte)(colors2[0].G * 0.5 + colors2[1].G * 0.5), (byte)(colors2[0].B * 0.5 + colors2[1].B * 0.5), (byte)(colors2[0].A * 0.5 + colors2[1].A * 0.5));
                        colors2[3] = (0, 0, 0, 0);
                    }
                    else
                    {
                        colors[2] = AddRGBAColors(MultiplyRGBAColors(colors[0], 0.66f), MultiplyRGBAColors(colors[1], 0.33f));
                        colors[3] = AddRGBAColors(MultiplyRGBAColors(colors[0], 0.33f), MultiplyRGBAColors(colors[1], 0.66f));

                        colors2[2] = ((byte)((colors2[0].R * 2 + colors2[1].R) / 3), (byte)((colors2[0].G * 2 + colors2[1].G) / 3), (byte)((colors2[0].B * 2 + colors2[1].B) / 3), 0xFF);
                        colors2[3] = ((byte)((colors2[1].R * 2 + colors2[0].R) / 3), (byte)((colors2[1].G * 2 + colors2[0].G) / 3), (byte)((colors2[1].B * 2 + colors2[0].B) / 3), 0xFF);

                        //colors2[2] = ((byte)((colors2[0].R * 0.66 + colors2[1].R * 0.33)), (byte)((colors2[0].G * 0.66 + colors2[1].G * 0.33)), (byte)((colors2[0].B * 0.66 + colors2[1].B * 0.33)), (byte)((colors2[0].A * 0.66 + colors2[1].A * 0.33)));
                        //colors2[3] = ((byte)((colors2[1].R * 0.66 + colors2[0].R * 0.33)), (byte)((colors2[1].G * 0.66 + colors2[0].G * 0.33)), (byte)((colors2[1].B * 0.66 + colors2[0].B * 0.33)), (byte)((colors2[1].A * 0.66 + colors2[0].A * 0.33)));
                    }

                    /*
                    // Indices - 4 bytes (16 pixels)
                    pixels[ 0] = colors[(raw[i + 4] & 0b00_00_00_11)     ]; // Row 1
                    pixels[ 1] = colors[(raw[i + 4] & 0b00_00_11_00) >> 2];
                    pixels[ 2] = colors[(raw[i + 4] & 0b00_11_00_00) >> 4];
                    pixels[ 3] = colors[(raw[i + 4] & 0b11_00_00_00) >> 6];

                    pixels[ 4] = colors[(raw[i + 5] & 0b00_00_00_11)     ]; // Row 2
                    pixels[ 5] = colors[(raw[i + 5] & 0b00_00_11_00) >> 2];
                    pixels[ 6] = colors[(raw[i + 5] & 0b00_11_00_00) >> 4];
                    pixels[ 7] = colors[(raw[i + 5] & 0b11_00_00_00) >> 6];

                    pixels[ 8] = colors[(raw[i + 6] & 0b00_00_00_11)     ]; // Row 3
                    pixels[ 9] = colors[(raw[i + 6] & 0b00_00_11_00) >> 2];
                    pixels[10] = colors[(raw[i + 6] & 0b00_11_00_00) >> 4];
                    pixels[11] = colors[(raw[i + 6] & 0b11_00_00_00) >> 6];

                    pixels[12] = colors[(raw[i + 7] & 0b00_00_00_11)     ]; // Row 4
                    pixels[13] = colors[(raw[i + 7] & 0b00_00_11_00) >> 2];
                    pixels[14] = colors[(raw[i + 7] & 0b00_11_00_00) >> 4];
                    pixels[15] = colors[(raw[i + 7] & 0b11_00_00_00) >> 6];
                    */

                    /*
                    colorRgba[ 0] = (byte)((colors[0] & 0xFF_00_00_00) >> 24);
                    colorRgba[ 1] = (byte)((colors[0] & 0x00_FF_00_00) >> 16);
                    colorRgba[ 2] = (byte)((colors[0] & 0x00_00_FF_00) >>  8);
                    colorRgba[ 3] = (byte)((colors[0] & 0x00_00_00_FF));

                    colorRgba[ 4] = (byte)((colors[1] & 0xFF_00_00_00) >> 24);
                    colorRgba[ 5] = (byte)((colors[1] & 0x00_FF_00_00) >> 16);
                    colorRgba[ 6] = (byte)((colors[1] & 0x00_00_FF_00) >>  8);
                    colorRgba[ 7] = (byte)((colors[1] & 0x00_00_00_FF));

                    colorRgba[ 8] = (byte)((colors[2] & 0xFF_00_00_00) >> 24);
                    colorRgba[ 9] = (byte)((colors[2] & 0x00_FF_00_00) >> 16);
                    colorRgba[10] = (byte)((colors[2] & 0x00_00_FF_00) >>  8);
                    colorRgba[11] = (byte)((colors[2] & 0x00_00_00_FF));

                    colorRgba[12] = (byte)((colors[3] & 0xFF_00_00_00) >> 24);
                    colorRgba[13] = (byte)((colors[3] & 0x00_FF_00_00) >> 16);
                    colorRgba[14] = (byte)((colors[3] & 0x00_00_FF_00) >>  8);
                    colorRgba[15] = (byte)((colors[3] & 0x00_00_00_FF));
                    */

                    colorRgba[ 0] = colors2[0].R;
                    colorRgba[ 1] = colors2[0].G;
                    colorRgba[ 2] = colors2[0].B;
                    colorRgba[ 3] = colors2[0].A;

                    colorRgba[ 4] = colors2[1].R;
                    colorRgba[ 5] = colors2[1].G;
                    colorRgba[ 6] = colors2[1].B;
                    colorRgba[ 7] = colors2[1].A;

                    colorRgba[ 8] = colors2[2].R;
                    colorRgba[ 9] = colors2[2].G;
                    colorRgba[10] = colors2[2].B;
                    colorRgba[11] = colors2[2].A;

                    colorRgba[12] = colors2[3].R;
                    colorRgba[13] = colors2[3].G;
                    colorRgba[14] = colors2[3].B;
                    colorRgba[15] = colors2[3].A;

                    // Indices - 4 bytes (16 pixels)
                    pixelIndices[ 0] = (raw[i + 4] & 0b00_00_00_11)     ; // Row 1
                    pixelIndices[ 1] = (raw[i + 4] & 0b00_00_11_00) >> 2;
                    pixelIndices[ 2] = (raw[i + 4] & 0b00_11_00_00) >> 4;
                    pixelIndices[ 3] = (raw[i + 4] & 0b11_00_00_00) >> 6;

                    pixelIndices[ 4] = (raw[i + 5] & 0b00_00_00_11)     ; // Row 2
                    pixelIndices[ 5] = (raw[i + 5] & 0b00_00_11_00) >> 2;
                    pixelIndices[ 6] = (raw[i + 5] & 0b00_11_00_00) >> 4;
                    pixelIndices[ 7] = (raw[i + 5] & 0b11_00_00_00) >> 6;

                    pixelIndices[ 8] = (raw[i + 6] & 0b00_00_00_11)     ; // Row 3
                    pixelIndices[ 9] = (raw[i + 6] & 0b00_00_11_00) >> 2;
                    pixelIndices[10] = (raw[i + 6] & 0b00_11_00_00) >> 4;
                    pixelIndices[11] = (raw[i + 6] & 0b11_00_00_00) >> 6;

                    pixelIndices[12] = (raw[i + 7] & 0b00_00_00_11)     ; // Row 4
                    pixelIndices[13] = (raw[i + 7] & 0b00_00_11_00) >> 2;
                    pixelIndices[14] = (raw[i + 7] & 0b00_11_00_00) >> 4;
                    pixelIndices[15] = (raw[i + 7] & 0b11_00_00_00) >> 6;

                    Array.Copy(colorRgba, pixelIndices[ 0] << 2, image, LinearOffset(x    , y    , width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 1] << 2, image, LinearOffset(x + 1, y    , width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 2] << 2, image, LinearOffset(x + 2, y    , width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 3] << 2, image, LinearOffset(x + 3, y    , width), 4);

                    Array.Copy(colorRgba, pixelIndices[ 4] << 2, image, LinearOffset(x    , y + 1, width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 5] << 2, image, LinearOffset(x + 1, y + 1, width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 6] << 2, image, LinearOffset(x + 2, y + 1, width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 7] << 2, image, LinearOffset(x + 3, y + 1, width), 4);

                    Array.Copy(colorRgba, pixelIndices[ 8] << 2, image, LinearOffset(x    , y + 2, width), 4);
                    Array.Copy(colorRgba, pixelIndices[ 9] << 2, image, LinearOffset(x + 1, y + 2, width), 4);
                    Array.Copy(colorRgba, pixelIndices[10] << 2, image, LinearOffset(x + 2, y + 2, width), 4);
                    Array.Copy(colorRgba, pixelIndices[11] << 2, image, LinearOffset(x + 3, y + 2, width), 4);

                    Array.Copy(colorRgba, pixelIndices[12] << 2, image, LinearOffset(x    , y + 3, width), 4);
                    Array.Copy(colorRgba, pixelIndices[13] << 2, image, LinearOffset(x + 1, y + 3, width), 4);
                    Array.Copy(colorRgba, pixelIndices[14] << 2, image, LinearOffset(x + 2, y + 3, width), 4);
                    Array.Copy(colorRgba, pixelIndices[15] << 2, image, LinearOffset(x + 3, y + 3, width), 4);

                    if (encoding == DxEncoding.DXGI_FORMAT_BC3_UNORM)
                    {
                        image[LinearOffset(x    , y    , width) + 3] = alphaPixels[ 0];
                        image[LinearOffset(x + 1, y    , width) + 3] = alphaPixels[ 1];
                        image[LinearOffset(x + 2, y    , width) + 3] = alphaPixels[ 2];
                        image[LinearOffset(x + 3, y    , width) + 3] = alphaPixels[ 3];
                        
                        image[LinearOffset(x    , y + 1, width) + 3] = alphaPixels[ 4];
                        image[LinearOffset(x + 1, y + 1, width) + 3] = alphaPixels[ 5];
                        image[LinearOffset(x + 2, y + 1, width) + 3] = alphaPixels[ 6];
                        image[LinearOffset(x + 3, y + 1, width) + 3] = alphaPixels[ 7];
                        
                        image[LinearOffset(x    , y + 2, width) + 3] = alphaPixels[ 8];
                        image[LinearOffset(x + 1, y + 2, width) + 3] = alphaPixels[ 9];
                        image[LinearOffset(x + 2, y + 2, width) + 3] = alphaPixels[10];
                        image[LinearOffset(x + 3, y + 2, width) + 3] = alphaPixels[11];

                        image[LinearOffset(x    , y + 3, width) + 3] = alphaPixels[12];
                        image[LinearOffset(x + 1, y + 3, width) + 3] = alphaPixels[13];
                        image[LinearOffset(x + 2, y + 3, width) + 3] = alphaPixels[14];
                        image[LinearOffset(x + 3, y + 3, width) + 3] = alphaPixels[15];
                    }

                    i += blockSize;
                }
            }

            return image;
        }

        private static byte[] EncodeDxImage(byte[] raw, int width, int height, int mips, DxEncoding encoding)
        {
            using var image = ImageWrapper.FromRGBA(raw, width, height);

            return encoding switch
            {
                DxEncoding.DXGI_FORMAT_BC1_UNORM => image.AsDXT1(),
                DxEncoding.DXGI_FORMAT_BC5_UNORM => image.AsDXT5(),
                _ => image.AsDXT5() // TODO: Support ATI2 somehow
            };

            /*
            using var ddsStream = new MemoryStream();
            var format = encoding switch
            {
                DxEncoding.DXGI_FORMAT_BC1_UNORM => MagickFormat.Dxt1,
                DxEncoding.DXGI_FORMAT_BC5_UNORM => MagickFormat.Dxt5,
                _ => MagickFormat.Dxt5 // TODO: Support ATI2 somehow
            };

            var bpp = encoding switch
            {
                DxEncoding.DXGI_FORMAT_BC1_UNORM => 4,
                DxEncoding.DXGI_FORMAT_BC5_UNORM => 8,
                _ => 8
            };

            image.Write(ddsStream, format);
            ddsStream.Seek(128, SeekOrigin.Begin);

            var dataSize = (width * height * bpp) / 8;
            var data = new byte[dataSize];
            ddsStream.Read(data, 0, data.Length);

            return data;*/
        }

        private static int LinearOffset(int x, int y, int w) => (y * (w << 2)) + (x << 2);

        private static byte _4BitsTo8Bits(int val, int andVal = 0x0F)
        {
            int rShift = 0;
            while (rShift < 32)
            {
                if ((andVal & (1 << rShift)) != 0)
                    break;

                rShift++;
            }

            val = (val & andVal) >> rShift;
            return (byte)(Math.Round((val / 15.0) * 255.0));
        }

        private static (byte, byte, byte, byte) RGBAFromRGB565(ushort c)
        {
            return ((byte)((((c & 0b1111_1000_0000_0000) << 16) | ((c & 0b1110_0000_0000_0000) << 11)) >> 24),
                    (byte)((((c & 0b0000_0111_1110_0000) << 13) | ((c & 0b0000_0110_0000_0000) <<  7)) >> 16),
                    (byte)((((c & 0b0000_0000_0001_1111) << 11) | ((c & 0b0000_0000_0001_1100) <<  6)) >>  8),
                    0xFF);
        }

        private static int ReadRGBAFromRGB565(ushort c)
        {
            return ((c & 0b1111_1000_0000_0000) << 16) | ((c & 0b1110_0000_0000_0000) << 11)
                |  ((c & 0b0000_0111_1110_0000) << 13) | ((c & 0b0000_0110_0000_0000) <<  7)
                |  ((c & 0b0000_0000_0001_1111) << 11) | ((c & 0b0000_0000_0001_1100) <<  6)
                | 0xFF;
        }
        
        private static int AddRGBAColors(int c1, int c2)
        {
#pragma warning disable CS0675
            return (int)((((c1 & 0xFF_00_00_00) + (c2 & 0xFF_00_00_00)) & 0xFF_00_00_00)
                      |  (((c1 & 0x00_FF_00_00) + (c2 & 0x00_FF_00_00)) & 0x00_FF_00_00)
                      |  (((c1 & 0x00_00_FF_00) + (c2 & 0x00_00_FF_00)) & 0x00_00_FF_00)
                      //|  (((c1 & 0x00_00_00_FF) + (c2 & 0x00_00_00_FF)) & 0x00_00_00_FF));
                      | 0xFF);
#pragma warning restore CS0675
        }

        private static int MultiplyRGBAColors(int c, float m)
        {
#pragma warning disable CS0675
            return (int)(((byte)(((c & 0xFF_00_00_00) >> 24) * m) << 24 & 0xFF_00_00_00)
                      |  ((byte)(((c & 0x00_FF_00_00) >> 16) * m) << 16 & 0x00_FF_00_00)
                      |  ((byte)(((c & 0x00_00_FF_00) >>  8) * m) <<  8 & 0x00_00_FF_00)
                      //|  ((int)((c & 0x00_00_00_FF) * m) & 0x00_00_00_FF));
                      | 0xFF);
#pragma warning restore CS0675
        }

        private static void ReadRGBAFromRGB565(byte[] rgba, int c)
        {
            rgba[0] = (byte)((c & 0b1111_1000_0000_0000) >> 8);
            rgba[1] = (byte)((c & 0b0000_0111_1110_0000) >> 3);
            rgba[2] = (byte)((c & 0b0000_0000_0001_1111) << 3);
            rgba[3] = 0xFF;
        }

        private static void AddRGBAColors(byte[] combined, byte[] c1, byte[] c2)
        {
            combined[0] = (byte)(c1[0] + c2[0]);
            combined[1] = (byte)(c1[1] + c2[1]);
            combined[2] = (byte)(c1[2] + c2[2]);
            combined[3] = (byte)(c1[3] + c2[3]);
        }

        private static byte[] Unpack24Bitndicies(int packed) =>
            new byte[]
            {
                (byte)( packed &  0b0111              ),
                (byte)((packed & (0b0111 <<  3)) >>  3),
                (byte)((packed & (0b0111 <<  6)) >>  6),
                (byte)((packed & (0b0111 <<  9)) >>  9),
                (byte)((packed & (0b0111 << 12)) >> 12),
                (byte)((packed & (0b0111 << 15)) >> 15),
                (byte)((packed & (0b0111 << 18)) >> 18),
                (byte)((packed & (0b0111 << 21)) >> 21)
            };

        private static byte[] InterpolateColors(byte c0, byte c1) =>
            (c0 > c1) ?
                new byte[]
                {
                    c0,
                    c1,
                    (byte)(((6.0 / 7.0) * c0) + ((1.0 / 7.0) * c1)),
                    (byte)(((5.0 / 7.0) * c0) + ((2.0 / 7.0) * c1)),
                    (byte)(((4.0 / 7.0) * c0) + ((3.0 / 7.0) * c1)),
                    (byte)(((3.0 / 7.0) * c0) + ((4.0 / 7.0) * c1)),
                    (byte)(((2.0 / 7.0) * c0) + ((5.0 / 7.0) * c1)),
                    (byte)(((1.0 / 7.0) * c0) + ((6.0 / 7.0) * c1))
                } :
                new byte[]
                {
                    c0,
                    c1,
                    (byte)(((4.0 / 5.0) * c0) + ((1.0 / 5.0) * c1)),
                    (byte)(((3.0 / 5.0) * c0) + ((2.0 / 5.0) * c1)),
                    (byte)(((2.0 / 5.0) * c0) + ((3.0 / 5.0) * c1)),
                    (byte)(((1.0 / 5.0) * c0) + ((4.0 / 5.0) * c1)),
                    0x00,
                    0xFF
                };

        private static byte[] UnpackIndexedInterpolatedColors(byte[] data, int i = 0)
        {
            byte[] pixels = new byte[16];

            var colors = InterpolateColors(data[i], data[i + 1]);

            var packed0 = (data[i + 4] << 16) | (data[i + 3] << 8) | (data[i + 2]);
            var packed1 = (data[i + 7] << 16) | (data[i + 6] << 8) | (data[i + 5]);

            byte[] inds = Unpack24Bitndicies(packed0);
            pixels[ 0] = colors[inds[0]];
            pixels[ 1] = colors[inds[1]];
            pixels[ 2] = colors[inds[2]];
            pixels[ 3] = colors[inds[3]];

            pixels[ 4] = colors[inds[4]];
            pixels[ 5] = colors[inds[5]];
            pixels[ 6] = colors[inds[6]];
            pixels[ 7] = colors[inds[7]];

            inds = Unpack24Bitndicies(packed1);
            pixels[ 8] = colors[inds[0]];
            pixels[ 9] = colors[inds[1]];
            pixels[10] = colors[inds[2]];
            pixels[11] = colors[inds[3]];

            pixels[12] = colors[inds[4]];
            pixels[13] = colors[inds[5]];
            pixels[14] = colors[inds[6]];
            pixels[15] = colors[inds[7]];

            return pixels;
        }

        public static void SaveAs(this HMXBitmap bitmap, SystemInfo info, string path)
        {
            if (bitmap == null || bitmap.RawData.Length <= 0) return;
            var rgba = bitmap.ToRGBA(info);

            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var image = ImageWrapper.FromRGBA(rgba, bitmap.Width, bitmap.Height);
            image.WriteToFile(path);
        }

        public static HMXBitmap BitmapFromImage(string imagePath, SystemInfo info)
        {
            using var image = new ImageWrapper(imagePath);

            var width = image.Width;
            var height = image.Height;

            // Checks width + height are powers of 2 and at least 4px
            // TODO: Use custom exception
            if ((width < 4)
                || ((width & (width - 1)) != 0)
                || (height < 4)
                || ((height & (height - 1)) != 0))
                throw new Exception($"Invalid image resolution of {width}x{height}. Both must be a power of 2 and at least 4px.");

            if (info.Platform == Platform.PS3
                || info.Platform == Platform.X360)
            {
                // TODO: Refactor to be more efficient
                var inputBytes = image.AsRGBA();

                // Encode as DXT5 for now
                var rawData = EncodeDxImage(inputBytes, width, height, 0, DxEncoding.DXGI_FORMAT_BC3_UNORM);
                var bpp2 = 8;

                if (info.Platform == Platform.X360)
                    SwapBytes(rawData);

                return new HMXBitmap()
                {
                    Bpp = bpp2,
                    Encoding = 24, // DXT5
                    MipMaps = 0,
                    Width = width,
                    Height = height,
                    BPL = (width * bpp2) / 8,
                    RawData = rawData
                };
            }

            var uniqueColors = image.GetUniqueColors();
            var hasAlpha = uniqueColors.Any(c => c.A < byte.MaxValue);

            byte[] data;
            int bpp = image switch
            {
                var img when info.Platform == Platform.PS2 && uniqueColors.Count <= 16 => 4,
                var img when info.Platform == Platform.PS2 && uniqueColors.Count <= 256 => 8,
                var img when info.Platform == Platform.PS2 && !hasAlpha => 24,
                _ => 32
            };
            //int bpp = 32;
            
            if (bpp <= 8)
            {
                // Use color palette
                var paletteSize = (1 << bpp) * 4;
                data = new byte[((width * height * bpp) / 8) + paletteSize];

                var i = 0;

                var paletteIndicies = new Dictionary<RGBAColor, int>();

                foreach (var c in uniqueColors)
                {
                    paletteIndicies.Add(c, i >> 2);

                    if (info.Platform != Platform.X360)
                    {
                        data[i    ] = c.R;
                        data[i + 1] = c.G;
                        data[i + 2] = c.B;
                        data[i + 3] = (c.A == 0xFF)
                            ? (byte)0x80
                            : (byte)(c.A >> 1);
                    }
                    else
                    {
                        // X360
                        data[i    ] = c.A;
                        data[i + 1] = c.R;
                        data[i + 2] = c.G;
                        data[i + 3] = c.B;
                    }

                    i += 4;
                }
                i = paletteSize;

                if (bpp == 8)
                {
                    foreach (var p in image.GetPixels())
                    {
                        var cIdx = paletteIndicies[p];
                        var bit3 = cIdx & 0b0000_1000;
                        var bit4 = cIdx & 0b0001_0000;

                        // TODO: Refactor completely or add check for x360? Not sure if x360 is bit shifted
                        cIdx = (cIdx & 0b1110_0111) | (bit3 << 1) | (bit4 >> 1);
                        data[i] = (byte)cIdx;
                        i += 1;
                    }
                }
                else // bpp = 4
                {
                    // Encodes two pixels into single byte
                    foreach (var p in image.GetPixels())
                    {
                        var cIdx = paletteIndicies[p]; // Color index
                        var dIdx = paletteSize + ((i - paletteSize) >> 1); // Data index
                        var dValue = data[dIdx]; // Data value

                        if ((i & 1) == 1)
                        {
                            data[dIdx] = (byte)((dValue & 0x0F) | (cIdx << 4));
                        }
                        else
                        {
                            data[dIdx] = (byte)((dValue & 0xF0) | cIdx);
                        }
                        i += 1;
                    }
                }
            }
            else
            {
                data = new byte[(width * height * bpp) / 8];
                var i = 0;

                if (bpp == 32)
                {
                    foreach (var c in image.GetPixels())
                    {
                        if (info.Platform != Platform.X360)
                        {
                            data[    i] = c.R;
                            data[i + 1] = c.G;
                            data[i + 2] = c.B;
                            data[i + 3] = (c.A == 0xFF)
                                ? (byte)0x80
                                : (byte)(c.A >> 1);
                        }
                        else
                        {
                            // X360
                            data[i    ] = c.A;
                            data[i + 1] = c.R;
                            data[i + 2] = c.G;
                            data[i + 3] = c.B;
                        }

                        i += 4;
                    }
                }
                else // bpp = 24
                {
                    foreach (var c in image.GetPixels())
                    {
                        data[i    ] = c.B;
                        data[i + 1] = c.G;
                        data[i + 2] = c.R;

                        i += 3;
                    }
                }
            }

            var bitmap = new HMXBitmap()
            {
                Bpp = bpp,
                Encoding = 3, // Bitmap
                MipMaps = 0,
                Width = width,
                Height = height,
                BPL = (width * bpp) / 8,
                RawData = data
            };

            return bitmap;
        }

        public static Tex TexFromImage(string imagePath, SystemInfo info)
        {
            var bitmap = BitmapFromImage(imagePath, info);

            return new Tex()
            {
                Name = $"{System.IO.Path.GetFileNameWithoutExtension(imagePath)}.tex",
                Width = bitmap.Width,
                Height = bitmap.Height,
                Bpp = bitmap.Bpp,
                IndexF = 0.0f,
                Index = 1,
                ExternalPath = System.IO.Path.GetFileName(imagePath),
                UseExternal = false,
                Bitmap = bitmap
            };
        }
    }
}
