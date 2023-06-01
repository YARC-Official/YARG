using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine;

namespace YARG.Serialization.Parser {
    public static class MiloParser {
        // used for milo parsing
        private static bool FoundPadding(List<byte> arr) {
            int lastIndex = arr.Count - 1;
            return arr[lastIndex - 3] == 0xAD && arr[lastIndex - 2] == 0xDE &&
                arr[lastIndex - 1] == 0xAD && arr[lastIndex] == 0xDE;
        }

        // inflate a milo file's bytes so that you can parse them
        private static byte[] Inflate(byte[] milo) {
            using var ms = new MemoryStream(milo);
            using var br = new BinaryReader(ms, new ASCIIEncoding());

            // 0xCABEDEAF = RBN
            // 0xCBBEDEAF = pre-RB3
            // 0xCDBEDEAF = RB3
            uint structureType = br.ReadUInt32();

            uint offset = br.ReadUInt32();
            int blockCount = br.ReadInt32();
            int maxBlockSize = br.ReadInt32();

            var sizes = new uint[blockCount];
            for (int i = 0; i < blockCount; i++) sizes[i] = br.ReadUInt32();

            ms.Seek(offset, SeekOrigin.Begin);

            var miloData = new List<byte>();
            foreach (var size in sizes) {
                byte[] block = br.ReadBytes((int)(size & 0x00FFFFFF));

                if (size <= 0x00FFFFFF) {
                    if (structureType == 0xCBBEDEAF) { // pre-RB3
                        using var ms2 = new MemoryStream(block, 0, block.Length);
                        using var zs = new DeflateStream(ms2, CompressionMode.Decompress);
                        using var res = new MemoryStream();
                        zs.CopyTo(res);
                        block = res.ToArray();
                    }
                    if (structureType == 0xCDBEDEAF) { // RB3
                        using var ms3 = new MemoryStream(block, 4, block.Length - 4);
                        using var zs = new DeflateStream(ms3, CompressionMode.Decompress);
                        using var res = new MemoryStream();
                        zs.CopyTo(res);
                        block = res.ToArray();
                    }
                }
                miloData.AddRange(block);
            }
            return miloData.ToArray();
        }

        // parse the fully inflated bytes for usable files
        private static Dictionary<string, byte[]> ParseMiloForFiles(byte[] milo){
            // inflate the milo
            byte[] inflated = Inflate(milo);
            var FileDict = new Dictionary<string, byte[]>();

            // with the milo file inflated, now we can properly begin to parse it
            using var ms = new MemoryStream(inflated);
            using var br = new BinaryReader(ms);

            int miloVersion = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
            br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip miloType
            br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip miloName
            br.ReadBytes(8); // skip miloU1 and miloU2

            // get entry names
            int entryCount = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
            var MiloEntryNames = new string[entryCount];
            for (int i = 0; i < entryCount; i++) {
                br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip MiloEntryType
                uint MiloEntryNameCnt = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                MiloEntryNames[i] = Encoding.UTF8.GetString(br.ReadBytes((int)MiloEntryNameCnt), 0, (int)MiloEntryNameCnt);
            }

            br.ReadBytes(4); // skip miloU3

            if (miloVersion == 28) {
                br.ReadBytes(4); // skip miloU4
                br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip miloSubname
                br.ReadBytes(8); // skip miloU5 and miloU6
            }

            br.ReadBytes(BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4)) * 48 + 9); // skip matrices and miloU7-9

            int parentCnt = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
            for(int i = 0; i < parentCnt; i++) br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip miloParents

            br.ReadBoolean(); // skip miloU10

            int childrenCnt = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
            for(int i = 0; i < childrenCnt; i++) br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip miloChildren
            if (childrenCnt > 0) br.ReadBytes(2); // skip miloU11

            for (int i = 0; i < childrenCnt; i++) {
                int subVersion = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip subMiloType
                br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip subMiloName
                br.ReadBytes(8); // skip miloU1 and miloU2

                int subEntryCount = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                var subMiloEntryNames = new string[subEntryCount];
                for (int k = 0; k < subEntryCount; k++) {
                    uint subMiloEntryTypeCnt = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                    br.ReadBytes((int)subMiloEntryTypeCnt); // skip subMiloEntryType
                    uint subMiloEntryNameCnt = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                    subMiloEntryNames[k] = Encoding.UTF8.GetString(br.ReadBytes((int)subMiloEntryNameCnt), 0, (int)subMiloEntryNameCnt);
                }

                br.ReadBytes(4); // skip subMiloU3

                if (subVersion == 28) {
                    br.ReadBytes(4); // skip subMiloU4
                    br.ReadBytes((int)BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4))); // skip subMiloSubnameCnt 
                    br.ReadBytes(8); // skip subMiloU5 and skipMiloU6
                }

                br.ReadBytes(BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4)) * 48 + 9); // skip matrices and miloU7-9

                int subParentCnt = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                if (subParentCnt > 0) throw new Exception("ERROR: should not have further parents within this subdir!");

                br.ReadBoolean(); // skip subMiloU10

                int subChildrenCnt = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                if(subChildrenCnt > 0) throw new Exception("ERROR: should not have further children within this subdir!");

                byte[] subRemaining = br.ReadBytes(17);
                if (!FoundPadding(subRemaining.ToList())) 
                    throw new Exception($"Expected padding AD-DE-AD-DE in remaining sub-bytes {BitConverter.ToString(subRemaining)}");
                
                for (int k = 0; k < subEntryCount; k++) {
                    var subFileBytes = new List<byte>();
                    subFileBytes.AddRange(br.ReadBytes(4));
                    while(!FoundPadding(subFileBytes)) subFileBytes.Add(br.ReadByte());
                    subFileBytes.RemoveRange(subFileBytes.Count - 4, 4);
                    FileDict.Add(subMiloEntryNames[k], subFileBytes.ToArray());
                }

            }

            var remaining = new List<byte>();
            remaining.AddRange(br.ReadBytes(17));
            while(!FoundPadding(remaining)) remaining.Add(br.ReadByte());

            for (int j = 0; j < entryCount; j++) {
                var fileBytes = new List<byte>();
                fileBytes.AddRange(br.ReadBytes(4));
                while(!FoundPadding(fileBytes)) fileBytes.Add(br.ReadByte());
                fileBytes.RemoveRange(fileBytes.Count - 4, 4);
                FileDict.Add(MiloEntryNames[j], fileBytes.ToArray());
            }

            return FileDict;
        }

        // the main fxn we actually care about
        // TODO: change from void return val to a List of TrackChunks
        public static void GetMidiFromMilo(byte[] miloBytes, TempoMap tmap){
            // inflate milo bytes
            // get dictionary of files and filebytes from inflated milo
            var MiloFiles = ParseMiloForFiles(miloBytes);

            // with our list of files and filebytes from the milo, we must convert each file to a MIDI track
            foreach(var f in MiloFiles){
                switch(f.Key){
                    case "song.anim":
                        Debug.Log("found song.anim file, must convert to midi VENUE track");
                        break;
                    case "song.lipsync":
                        Debug.Log("found song.lipsync file, must convert to midi LIPSYNC1 track");
                        break;
                    case "part2.lipsync":
                        Debug.Log("found part2.lipsync file, must convert to midi LIPSYNC2 track");
                        break;
                    case "part3.lipsync":
                        Debug.Log("found part3.lipsync file, must convert to midi LIPSYNC3 track");
                        break;
                    case "part4.lipsync":
                        Debug.Log("found part4.lipsync file, must convert to midi LIPSYNC4 track");
                        break;
                    default: break;
                }
            }
        }
    }
}