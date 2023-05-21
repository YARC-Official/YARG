using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

// The below code was originally written by Arkem in Python, on this repo: https://github.com/arkem/py360
// For the purposes of YARG, the relevant code has been ported to C#,
// so that YARG can read the contents of a Rock Band CON file (.mid, .mogg, .dta, .png_xbox, etc)

namespace XboxSTFS {
    // Object containing the information about a file in the STFS container
    public class FileListing {
        public string filename { get; set; }
        public byte flags { get; set; }
        public uint numBlocks { get; set; }
        public uint firstBlock { get; set; }
        public uint size { get; set; }
        public short pathIndex { get; set; }

        public FileListing(byte[] data){
            filename = System.Text.Encoding.UTF8.GetString(data, 0, 0x28).TrimEnd('\0');
            flags = data[0x28];
            
            numBlocks = BitConverter.ToUInt32(new byte[4] { data[0x29], data[0x2A], data[0x2B], 0x00 });
            firstBlock = BitConverter.ToUInt32(new byte[4] { data[0x2F], data[0x30], data[0x31], 0x00 });
            pathIndex = BitConverter.ToInt16(new byte[2] { data[0x33], data[0x32] });
            size = BitConverter.ToUInt32(new byte[4] { data[0x37], data[0x36], data[0x35], data[0x34] });
        }

        public FileListing(){}

        public override string ToString() => $"STFS File Listing: {filename}";
        public bool IsDirectory() { return (flags & 0x80) > 0; }
        public bool IsContiguous() { return (flags & 0x40) > 0; }
    }

    public static class XboxSTFSParser {

        private static uint FixBlocknum(uint blocknum, bool shift){
            // Given a blocknumber calculate the block on disk that has the data taking into account hash blocks.
            // Every 0xAA blocks there is a hash table and depending on header data it
            // is 1 or 2 blocks long [((self.entry_id+0xFFF) & 0xF000) >> 0xC 0xB == 0, 0xA == 1]
            // After 0x70e4 blocks there is another table of the same size every 0x70e4
            // blocks and after 0x4af768 blocks there is one last table. This skews blocknumber to offset calcs.
            // This is the part of the Free60 STFS page that needed work
            uint blockAdjust = 0;
            byte tableSizeShift = Convert.ToByte(shift);

            if (blocknum >= 0xAA) blockAdjust += ((blocknum / 0xAA) + 1) << tableSizeShift;
            if (blocknum >= 0x70E4) blockAdjust += ((blocknum / 0x70E4) + 1) << tableSizeShift;
            return blockAdjust + blocknum;
        }

        // given a CON file, retrieve the relevant FileListings for easy file extraction later
        public static Dictionary<string, FileListing> GetCONFileListings(string CONName){
            var fs = new FileStream(CONName, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            string magic = new string(new BinaryReader(fs, new ASCIIEncoding()).ReadChars(4));
            Assert.IsTrue((magic == "CON " || magic == "PIRS" || magic == "LIVE"), "STFS Magic not found");
            fs.Seek(0, SeekOrigin.Begin);

            // parse header for relevant info
            var header = br.ReadBytes(0x971A);
            Assert.IsTrue(header.Length >= 0x971A, "STFS header data too short");
            uint entryID = BitConverter.ToUInt32(new byte[4] { header[0x343], header[0x342], header[0x341], header[0x340] });
            ushort fileTableBlockCount = BitConverter.ToUInt16(new byte[2] { header[0x37C], header[0x37D] });
            uint fileTableBlockNumber = BitConverter.ToUInt32(new byte[4] { header[0x37E], header[0x37F], header[0x380], 0x00 });
            bool shiftTable = (((entryID + 0xFFF) & 0xF000) >> 0xC != 0xB);

            // parse file table
            var fLists = new List<FileListing>();
            var allFiles = new Dictionary<string, FileListing>();
            // retrieve file table bytes
            uint dataSize = (uint) 0x1000*fileTableBlockCount;
            byte[] data = new byte[dataSize];
            uint i = 0;
            while(i < fileTableBlockCount){
                uint block = fileTableBlockNumber + i;
                fs.Seek(0xC000 + FixBlocknum(block,shiftTable)*0x1000, SeekOrigin.Begin);
                uint readCount = 170 - (block % 170);
                uint readSize = 0x1000 * readCount;
                uint offset = i * 0x1000;
                if(readSize > dataSize - offset) readSize = dataSize - offset;
                br.Read(data, (int) offset, (int) readSize);
                i += readCount;
            }
            // parse and retrieve FileListing entries
            byte[] buf = new byte[0x40];
            for (int x = 0; x < data.Length; x += 0x40) {
                Array.Copy(data, x, buf, 0, 0x40);
                FileListing file = new FileListing(buf);
                if (file.filename.Length == 0) break;
                fLists.Add(file);
            }

            List<string> pathComponents = new List<string>();
            foreach (FileListing fl in fLists) {
                pathComponents.Clear();
                pathComponents.Add(fl.filename);
                FileListing a = fl;

                while (a.pathIndex != -1 && a.pathIndex < fLists.Count) {
                    try {
                        a = fLists[a.pathIndex];
                        pathComponents.Add(a.filename);
                    } catch (Exception) {
                        Debug.Log($"Indexing Error: {CONName} {a.pathIndex} {fLists.Count}");
                    }
                }
                pathComponents.Add("");
                pathComponents.Reverse();
                allFiles[Path.Combine(pathComponents.ToArray())] = fl;
            }
            
            return allFiles;
        }

        private static byte[] ReadBlocksSeparate(ref FileStream fs, ref BinaryReader br, FileListing fl, bool shift) {
            byte[] fileBytes = new byte[fl.size];
            for (uint i = 0; i < fl.numBlocks; ++i) {
                fs.Seek(0xC000 + FixBlocknum(fl.firstBlock + i, shift) * 0x1000, SeekOrigin.Begin);
                if (i < fl.numBlocks - 1) br.Read(fileBytes, (int) i * 0x1000, 0x1000);
                else br.Read(fileBytes, (int) i * 0x1000, (int) fl.size % 0x1000);
            }
            return fileBytes;
        }

        private static byte[] ReadBlocksContiguous(ref FileStream fs, ref BinaryReader br, FileListing fl, bool shift) {
            byte[] fileBytes = new byte[fl.size];
            uint i = 0;
            while(i < fl.numBlocks){
                uint block = fl.firstBlock + i;
                fs.Seek(0xC000 + FixBlocknum(block, shift) * 0x1000, SeekOrigin.Begin);
                uint readCount = 170 - (block % 170);
                uint readSize = 0x1000 * readCount;
                uint offset = i * 0x1000;
                if (readSize > fl.size - offset) readSize = fl.size - offset;
                br.Read(fileBytes, (int) offset, (int) readSize);
                i += readCount;
            }
            return fileBytes;
        }

        public static byte[] GetFile(string CONName, FileListing fl){
            var fs = new FileStream(CONName, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            fs.Seek(0x340, SeekOrigin.Begin);
            var buf = br.ReadBytes(4);
            uint entryID = BitConverter.ToUInt32(new byte[4] { buf[3], buf[2], buf[1], buf[0] });
            bool shiftTable = (((entryID + 0xFFF) & 0xF000) >> 0xC != 0xB);
            if(fl.IsContiguous()) return ReadBlocksContiguous(ref fs, ref br, fl, shiftTable);
            else return ReadBlocksSeparate(ref fs, ref br, fl, shiftTable);
        }

        public static byte[] GetMoggHeader(string CONName, FileListing fl){
            var fs = new FileStream(CONName, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            fs.Seek(0x340, SeekOrigin.Begin);
            var buf = br.ReadBytes(4);
            uint entryID = BitConverter.ToUInt32(new byte[4] { buf[3], buf[2], buf[1], buf[0] });
            bool shiftTable = (((entryID + 0xFFF) & 0xF000) >> 0xC != 0xB);
            fs.Seek(0xC000 + FixBlocknum(fl.firstBlock, shiftTable) * 0x1000, SeekOrigin.Begin);
            return br.ReadBytes(8);
        }
    }

    // static class STFS:
    //     static Dictionary<string, FileListing> GetCONFileListings(CONname)
    //         this fxn parses the header and file table, just as it does now
    //     static byte[] GetFile(CONname, FileListing)
    //         this fxn will also parse the entryID to determine the tableShiftSize
    //         because tableShiftSize is used in FixBlocknum
}