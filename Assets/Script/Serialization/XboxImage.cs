using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
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
                    for(int i = 0; i < DXTBlocks.Length; i += 2){
                        byte temp = DXTBlocks[i];
                        DXTBlocks[i] = DXTBlocks[i+1];
                        DXTBlocks[i+1] = temp;
                    }

                    imageBytes = new byte[width*height*4];
                    XboxImageParser.BlockDecompressXboxImage((uint)width, (uint)height, ((bitsPerPixel == 0x04) && (format == 0x08)), DXTBlocks, imageBytes);
                }
            }
        }

        public byte[] getImage() { return imageBytes; }

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