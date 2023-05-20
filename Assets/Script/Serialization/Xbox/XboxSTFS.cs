using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

/*
    Copyright 2011 Arkem. All rights reserved.

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
    IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
    (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
    HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

    The below code was originally written by Arkem in Python, on this repo: https://github.com/arkem/py360
    For the purposes of YARG, I (rjkiv on GitHub) have ported the necessary code to C#,
    so that YARG can read the contents of a Rock Band CON file (.mid, .mogg, .dta, .png_xbox, etc)

    Further parsing improvements provided by Sonicfind (also github)
*/

namespace XboxSTFS {

    public static class STFSHashInfo {
        // Whether the block represented by the BlockHashRecord is used, free, old or current
        public static readonly Dictionary<byte, string> Types = new Dictionary<byte, string>(){
            { 0x00, "Unused" }, { 0x40, "Freed" }, { 0x80, "Old" }, { 0xC0, "Current" }
        };
        public static readonly string[] TypesList = { "Unused", "Allocated Free", "Allocated In Use Old", "Allocated In Use Current" };
    }

    // Object containing the information about a file in the STFS container
    // Data includes size, name, path and firstblock and atime and utime
    public class FileListing {
        public string filename { get; private set; }
        public byte flags { get; private set; }
        public uint numBlocks { get; private set; }
        public uint firstBlock { get; private set; }
        public uint size { get; private set; }
        public short pathIndex { get; private set; }

        public FileListing(byte[] data){
            filename = System.Text.Encoding.UTF8.GetString(data, 0, 0x28).TrimEnd('\0');
            flags = data[0x28];
            
            numBlocks = BitConverter.ToUInt32(new byte[4] { data[0x29], data[0x2A], data[0x2B], 0x00 });
            firstBlock = BitConverter.ToUInt32(new byte[4] { data[0x2F], data[0x30], data[0x31], 0x00 });
            pathIndex = BitConverter.ToInt16(new byte[2] { data[0x33], data[0x32] });
            size = BitConverter.ToUInt32(new byte[4] { data[0x37], data[0x36], data[0x35], data[0x34] });
        }

        public override string ToString() => $"STFS File Listing: {filename}";
        public bool isDirectory() { return (flags & 0x80) > 0; }
        public bool isContinguous() { return (flags & 0x40) > 0; }
    }

    public class STFS {
        private string filename, magic;
        private byte tableSizeShift = 0;
        private uint entryID, titleID, fileTableBlockNumber, allocatedCount;
        private ushort fileTableBlockCount;
        private int[,] tableSpacing = new int[2, 3] { {0xAB, 0x718F, 0xFE7DA}, {0xAC, 0x723A, 0xFD00B} };
        private Dictionary<string, FileListing> allFiles;

        public STFS(string fname){
            filename = fname;
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            magic = new string(new BinaryReader(fs, new ASCIIEncoding()).ReadChars(4));
            Assert.IsTrue((magic == "CON " || magic == "PIRS" || magic == "LIVE"), "STFS Magic not found");
            fs.Seek(0, SeekOrigin.Begin);
            ParseHeader(ref br);
            ParseFileTable(ref fs, ref br);
        }

        private byte[] ReadFileTable(ref FileStream fs, ref BinaryReader br, uint firstBlock, ushort numBlocks) {
            return ReadBlocks_Contiguous(ref fs, ref br, firstBlock, numBlocks, (uint) 0x1000 * numBlocks);
        }

        private void ParseFileTable(ref FileStream fs, ref BinaryReader br) {
            List<FileListing> fLists = new List<FileListing>();
            allFiles = new Dictionary<string, FileListing>();
            var data = ReadFileTable(ref fs, ref br, fileTableBlockNumber, fileTableBlockCount);

            byte[] buf = new byte[0x40];
            for (int x = 0; x < data.Length; x += 0x40) {
                Array.Copy(data, x, buf, 0, 0x40);
                FileListing file = new FileListing(buf);
                if (file.filename.Length == 0)
                    break;
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
                    } catch (Exception ee) {
                        Debug.Log($"Indexing Error: {filename} {a.pathIndex} {fLists.Count}");
                    }
                }
                pathComponents.Add("");
                pathComponents.Reverse();
                allFiles[Path.Combine(pathComponents.ToArray())] = fl;
            }
        }

        private uint FixBlocknum(uint blocknum) {
            // Given a blocknumber calculate the block on disk that has the data taking into account hash blocks.
            // Every 0xAA blocks there is a hash table and depending on header data it
            // is 1 or 2 blocks long [((self.entry_id+0xFFF) & 0xF000) >> 0xC 0xB == 0, 0xA == 1]
            // After 0x70e4 blocks there is another table of the same size every 0x70e4
            // blocks and after 0x4af768 blocks there is one last table. This skews blocknumber to offset calcs.
            // This is the part of the Free60 STFS page that needed work
            uint blockAdjust = 0;

            if (blocknum >= 0xAA) blockAdjust += ((blocknum / 0xAA) + 1) << tableSizeShift;
            if (blocknum >= 0x70E4) blockAdjust += ((blocknum / 0x70E4) + 1) << tableSizeShift;
            return blockAdjust + blocknum;
        }

        private byte[] ReadBlocks_Separate(ref FileStream fs, ref BinaryReader br, uint firstBlock, uint numBlocks, uint fileSize) {
            byte[] fileBytes = new byte[fileSize];
            for (uint i = 0; i < numBlocks; ++i) {
                fs.Seek(0xC000 + FixBlocknum(firstBlock + i) * 0x1000, SeekOrigin.Begin);
                if (i < numBlocks - 1)
                    br.Read(fileBytes, (int) i * 0x1000, 0x1000);
                else
                    br.Read(fileBytes, (int) i * 0x1000, (int) fileSize % 0x1000);
            }
            return fileBytes;
        }

        private byte[] ReadBlocks_Contiguous(ref FileStream fs, ref BinaryReader br, uint blocknum, uint blockCount, uint fileSize) {
            byte[] fileBytes = new byte[fileSize];
            for (uint i = 0; i < blockCount;) {
                uint block = blocknum + i;
                fs.Seek(0xC000 + FixBlocknum(block) * 0x1000, SeekOrigin.Begin);
                uint readCount = 170 - (block % 170);
                uint readSize = 0x1000 * readCount;
                uint offset = i * 0x1000;
                if (readSize > fileSize - offset)
                    readSize = fileSize - offset;

                br.Read(fileBytes, (int) offset, (int) readSize);
                i += readCount;
            }
            return fileBytes;
        }

        private void ParseHeader(ref BinaryReader br) {
            var data = br.ReadBytes(0x971A);
            // parse the huge STFS header
            Assert.IsTrue(data.Length >= 0x971A, "STFS Data too short");

            entryID = BitConverter.ToUInt32(new byte[4] { data[0x343], data[0x342], data[0x341], data[0x340] });
            titleID = BitConverter.ToUInt32(new byte[4] { data[0x363], data[0x362], data[0x361], data[0x360] });
            fileTableBlockCount = BitConverter.ToUInt16(new byte[2] { data[0x37C], data[0x37D] });
            fileTableBlockNumber = BitConverter.ToUInt32(new byte[4] { data[0x37E], data[0x37F], data[0x380], 0x00 });
            allocatedCount = BitConverter.ToUInt32(new byte[4] { data[0x398], data[0x397], data[0x396], data[0x395] });

            if (((entryID + 0xFFF) & 0xF000) >> 0xC != 0xB)
                tableSizeShift = 1;
        }

        public override string ToString() => $"STFS Object {magic} ({filename})";

        private byte[] ReadFile(ref FileStream fs, ref BinaryReader br, FileListing fl) {
            if (fl.isContinguous())
                return ReadBlocks_Contiguous(ref fs, ref br, fl.firstBlock, fl.numBlocks, fl.size);
            else
                return ReadBlocks_Separate(ref fs, ref br, fl.firstBlock, fl.numBlocks, fl.size);
        }

        public void ExtractAllContents(){
            foreach(var f in allFiles){
                if(allFiles[f.Key].isDirectory()){
                    Debug.Log($"Creating directory: {f.Key}");
                    Directory.CreateDirectory(Path.Combine($"{filename}_out", f.Key));
                }
            }

            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            foreach (var f in allFiles){
                if(!allFiles[f.Key].isDirectory()){
                    Debug.Log($"Writing file: {f.Key}");
                    try{
                        File.WriteAllBytes(Path.Combine($"{filename}_out", f.Key), ReadFile(ref fs, ref br, allFiles[f.Key]));
                    }
                    catch(Exception e){
                        Debug.Log(e);
                        Debug.Log($"Could not write file {Path.Combine($"{filename}_out", f.Key)}");
                    }
                }
            }
        }

        // used for benchmarking
        public void MockExtract(){
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            var stopWatch = new System.Diagnostics.Stopwatch();
            var total = new System.Diagnostics.Stopwatch();
            total.Start();
            foreach(var f in allFiles){
                if(!allFiles[f.Key].isDirectory()){
                    stopWatch.Restart();
                    byte[] testFile = ReadFile(ref fs, ref br, allFiles[f.Key]);
                    stopWatch.Stop();
                    Debug.Log($"read {testFile.Length} byte file {f.Key} in {stopWatch.ElapsedMilliseconds} ms.");
                }
            }
            total.Stop();
            Debug.Log($"time taken to read all files in {filename}: {total.ElapsedMilliseconds / 1000} s.");
        }

        public byte[] GetFile(string fname){
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs, new ASCIIEncoding());
            foreach (var f in allFiles){
                if(f.Key == fname){
                    return ReadFile(ref fs, ref br, allFiles[f.Key]);
                }
            }
            return null;
        }

        public uint GetFileSize(string fname){
            foreach(var f in allFiles)
                if(f.Key == fname)
                    return allFiles[f.Key].size;
            return 0;
        }

        public uint[] GetMemOffsets(string fname){
            foreach(var f in allFiles){
                if(f.Key == fname){
                    FileListing fl = allFiles[f.Key];
                    uint lastSize = fl.size % 0x1000;
                    uint[] offsets = new uint[fl.numBlocks];
                    Parallel.For(0, fl.numBlocks, i => {
                        offsets[i] = 0xC000 + FixBlocknum((uint)(fl.firstBlock + i)) * 0x1000;
                    });
                    return offsets;
                }
            }
            return null;
        }
    }
}