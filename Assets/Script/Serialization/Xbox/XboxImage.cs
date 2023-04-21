using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Serialization;

namespace YARG.Serialization {
    public class XboxImage {
        private byte game, bitsPerPixel, mipmaps;
        private int format;
        private short width, height, bytesPerLine;
        private string imagePath;
        private byte[] imageBytes;

        public XboxImage(string str){ imagePath = str; }

        public void ParseImageHeader(){
            using(FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read)){
                using(BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())){
                    // parse header
                    byte[] header = br.ReadBytes(32);
                    game = header[0];
                    bitsPerPixel = header[1];
                    format = BitConverter.ToInt32(header, 2);
                    mipmaps = header[6];
                    width = BitConverter.ToInt16(header, 7);
                    height = BitConverter.ToInt16(header, 9);
                    bytesPerLine = BitConverter.ToInt16(header, 11);
                    // // parse DXT-compressed blocks
                    // byte[] DXTBlocks = br.ReadBytes((int)(fs.Length - 32));
                }
            }
        }

        // return byte array of DXT1 formatted blocks to make into a Unity Texture
        public byte[] GetDXTBlocksFromImage(){
            using(FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read)){
                using(BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())){
                    // skip header
                    byte[] header = br.ReadBytes(32);
                    // parse DXT-compressed blocks, depending on format
                    byte[] DXTBlocks;
                    // if DXT-1 format already, read the bytes straight up
                    if((bitsPerPixel == 0x04) && (format == 0x08))
                        DXTBlocks = br.ReadBytes((int)(fs.Length - 32));
                    // if DXT-3 format, we have to omit the alpha bytes
                    else{
                        DXTBlocks = new byte[(fs.Length - 32) / 2];
                        byte[] buf = new byte[8];
                        for(int i = 0; i < DXTBlocks.Length; i += 8){
                            buf = br.ReadBytes(8); // skip over every 8 bytes
                            buf = br.ReadBytes(8); // we want to read these 8 bytes
                            for(int j = 0; j < 8; j++) DXTBlocks[i + j] = buf[j];
                        }
                    }
                    // swap bytes because xbox is weird like that
                    Parallel.For(0, DXTBlocks.Length/2, i => {
                        (DXTBlocks[i*2], DXTBlocks[i*2 + 1]) = (DXTBlocks[i*2 + 1], (DXTBlocks[i*2]));
                    });
                    return DXTBlocks;
                }
            }
        }

        //will most likely remove this fxn - was for FAFO-ing
        public void ParseImage(){
            using(FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read)){
                using(BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())){
                    // parse header
                    byte[] header = br.ReadBytes(32);
                    game = header[0];
                    bitsPerPixel = header[1];
                    format = BitConverter.ToInt32(header, 2);
                    mipmaps = header[6];
                    width = BitConverter.ToInt16(header, 7);
                    height = BitConverter.ToInt16(header, 9);
                    bytesPerLine = BitConverter.ToInt16(header, 11);
                    // parse DXT-compressed blocks
                    byte[] DXTBlocks = br.ReadBytes((int)(fs.Length - 32));
                    // swap bytes because xbox is weird like that
                    Parallel.For(0, DXTBlocks.Length/2, i => {
                        (DXTBlocks[i*2], DXTBlocks[i*2 + 1]) = (DXTBlocks[i*2 + 1], (DXTBlocks[i*2]));
                    });

                    imageBytes = new byte[width*height*4];
                    XboxImageParser.BlockDecompressXboxImage((uint)width, (uint)height, ((bitsPerPixel == 0x04) && (format == 0x08)), DXTBlocks, imageBytes);
                }
            }
        }

        // will remove this fxn too - not efficient to save image bytes directly to memory
        public byte[] getImage() { return imageBytes; }
        // this too
        public bool SaveImageToDisk(string fname){
            if(imageBytes == null) return false;
            else{
                var fmt = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
                Bitmap bmp = new Bitmap(width, height, fmt);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, fmt);
                System.Runtime.InteropServices.Marshal.Copy(imageBytes, 0, data.Scan0, imageBytes.Length);
                bmp.UnlockBits(data);
                bmp.Save($"{fname}.png", ImageFormat.Png);
                Debug.Log("image has been written");
                return true;
            }
        }        
    }
}