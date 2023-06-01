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

// parses RB style milos to extract their venue and lipsync information
// kudos to PikminGuts92 for his 010 templates, making it much easier to understand how to parse these things
// as well as AddyMills for his original RB-Tools scripts porting from .anim and .lipsync files to midi
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

        // search a byte array "pattern" for a specific byte combination "src"
        private static int Search(byte[] src, byte[] pattern) {
            int maxFirstCharSlot = src.Length - pattern.Length + 1;
            for (int i = 0; i < maxFirstCharSlot; i++) {
                if (src[i] != pattern[0]) // compare only first byte
                    continue;

                // found a match on first byte, now try to match rest of the pattern
                for (int j = pattern.Length - 1; j >= 1; j--) {
                    if (src[i + j] != pattern[j]) break;
                    if (j == 1) return i;
                }
            }
            return -1;
        }

        private static readonly string[] AnimParts = { "lightpreset_interp", "lightpreset_keyframe_interp", "world_event",   
            "spot_guitar", "spot_bass", "spot_drums", "spot_keyboard", "spot_vocal", "part2_sing", "part3_sing", "part4_sing", 
            "postproc_interp", "shot_bg", "stagekit_fog" };

        private static TrackChunk AnimToMidi(byte[] animBytes, TempoMap tmap){
            using var ms = new MemoryStream(animBytes);
            using var br = new BinaryReader(ms);
            
            var eventsDict = new Dictionary<string, List<(long time, string name)>>(); // time in ticks

            // capture events' names and time in ticks when they occur
            foreach (var part in AnimParts) {
                var eventsList = new List<(long time, string name)>();
                int offset = Search(animBytes, Encoding.UTF8.GetBytes(part));

                if (offset != -1) {
                    offset += part.Length + (part.EndsWith("interp") ? 5 : 13);

                    ms.Seek(offset, SeekOrigin.Begin);
                    int events = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                    bool listIsActive = true;

                    for (int i = 0; i < events; i++) {
                        if (part == "postproc_interp") br.ReadBytes(4);
                        int eventLen = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
                        string eventName = "";

                        if (eventLen > 0) eventName = Encoding.UTF8.GetString(br.ReadBytes(eventLen), 0, eventLen);
                        else if (eventsList.Count == 0) listIsActive = false;
                        else eventName = eventsList.Last().name;

                        var timeBuf = br.ReadBytes(4);
                        Array.Reverse(timeBuf);
                        double timeAdd = BitConverter.ToSingle(timeBuf) / 30; 
                        if (timeAdd < 0) timeAdd = 0;
                        // convert timeAdd from seconds to ticks
                        long timeInTicks = TimeConverter.ConvertFrom(new MetricTimeSpan((long)(timeAdd * 1000000)), tmap); 
                        if (listIsActive) eventsList.Add((timeInTicks, eventName));
                    }
                }
                eventsDict.Add(part, eventsList);
            }

            //now that we have the eventsDict, we can make the VENUE track
            var tracksToMerge = new List<TrackChunk>();
            bool trackNameExists = false;

            foreach(var part in AnimParts) {
                long timeStart = 0;
                var tempEvents = new List<MidiEvent>();
                var prevType = "note_off";
                foreach (var eventInstance in eventsDict[part]) {

                    long timeVal = eventInstance.time - timeStart;
                    int noteVal = 0;

                    if (part.EndsWith("_sing") || part.StartsWith("spot_")) {
                        noteVal = part switch {
                            "part2_sing" => 87,
                            "part3_sing" => 85,
                            "part4_sing" => 86,
                            "spot_keyboard" => 41,
                            "spot_vocal" => 40,
                            "spot_guitar" => 39,
                            "spot_drums" => 38,
                            "spot_bass" => 37,
                            _ => throw new Exception($"Unknown singalong or spotlight event found at tick count {eventInstance.time}!"),
                        };
                        if (eventInstance.name.EndsWith("on")) {
                            if(prevType == "note_on") {
                                tempEvents.Add(new NoteOffEvent() { NoteNumber = (SevenBitNumber)noteVal, Velocity = (SevenBitNumber)0, DeltaTime = timeVal });
                                timeStart += timeVal;
                                tempEvents.Add(new NoteOnEvent() { NoteNumber = (SevenBitNumber)noteVal, Velocity = (SevenBitNumber)100, DeltaTime = 0 });
                            }
                            else tempEvents.Add(new NoteOnEvent() { NoteNumber = (SevenBitNumber)noteVal, Velocity = (SevenBitNumber)100, DeltaTime = timeVal });
                            prevType = "note_on";
                        }
                        else if(eventInstance.name.EndsWith("off")) {
                            tempEvents.Add(new NoteOffEvent() { NoteNumber = (SevenBitNumber)noteVal, Velocity = (SevenBitNumber)0, DeltaTime = timeVal });
                            prevType = "note_off";
                        }
                        else throw new Exception($"Unknown state event found at tick count {eventInstance.time}");
                    }
                    else if(part == "lightpreset_interp") 
                        tempEvents.Add(new TextEvent($"[lighting ({eventInstance.name})]") { DeltaTime = timeVal });
                    else if(part == "stagekit_fog") 
                        tempEvents.Add(new TextEvent($"[Fog{char.ToUpper(eventInstance.name[0]) + eventInstance.name[1..]}]") { DeltaTime = timeVal });
                    else tempEvents.Add(new TextEvent($"[{eventInstance.name}]") { DeltaTime = timeVal });

                    timeStart += timeVal;
                }
                if (!trackNameExists) {
                    tempEvents.Insert(0, new SequenceTrackNameEvent("VENUE"));
                    trackNameExists = true;
                }
                tracksToMerge.Add(new TrackChunk(tempEvents));
            }
            return Melanchall.DryWetMidi.Core.TrackChunkUtilities.Merge(tracksToMerge);
        }

        private static TrackChunk LipsyncToMidi(byte[] lipBytes, TempoMap tmap, string trackName){
            using var ms = new MemoryStream(lipBytes);
            using var br = new BinaryReader(ms);

            ms.Seek(8, SeekOrigin.Begin);
            int offset = BitConverter.ToInt32(br.ReadBytes(4)) + 17;

            ms.Seek(offset, SeekOrigin.Begin);
            int visemeCount = BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));

            var visemes = new string[visemeCount];

            for(int i = 0; i < visemeCount; i++) {
                uint visemeNameLen = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                visemes[i] = Encoding.UTF8.GetString(br.ReadBytes((int)visemeNameLen), 0, (int)visemeNameLen);
                if (visemes[i].StartsWith("Exp") || visemes[i].EndsWith("Accent")) visemes[i] = visemes[i].ToLower();
            }

            uint frameCount = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));

            br.ReadBytes(4); // skip unknown bytes

            var lipsyncData = new (byte changes, byte[] byteArr)[frameCount];
            for(int x = 0; x < frameCount; x++) {
                byte cur = br.ReadByte();
                lipsyncData[x] = (cur, (cur != 0) ? br.ReadBytes(cur * 2) : null!);
            }
            
            var visemeFrame = new byte[visemeCount];
            var prevFrame = new byte[visemeCount];

            var visemeState = new List<List<byte>>(); // List of length frameCount (List of length visemeFrame)
            
            foreach(var lip in lipsyncData) {
                if(lip.changes != 0) {
                    int visemeEdit = 0;
                    for(int y = 0; y < lip.changes * 2; y++) {
                        if (y % 2 == 0) visemeEdit = lip.byteArr[y];
                        else visemeFrame[visemeEdit] = lip.byteArr[y];
                    }
                }
                visemeState.Add(new List<byte>(visemeFrame));
            }

            var lipsyncTrack = new List<MidiEvent> { new SequenceTrackNameEvent(trackName) };
            long timeStart = 0;
            
            for (int y = 0; y < visemeState.Count; y++) {
                double secs = y / 30.0;
                long secsInTicks = TimeConverter.ConvertFrom(new MetricTimeSpan((long)(secs * 1000000)), tmap);
                long timeVal = secsInTicks - timeStart;

                if (timeVal < 0) throw new Exception("oopsie doopsie");
                
                if(!Enumerable.SequenceEqual(prevFrame, visemeState[y])) {
                    timeStart += timeVal;
                    for(int i = 0; i < visemeState[y].Count; i++) {
                        if (visemeState[y][i] != prevFrame[i]) {
                            string textEvent = $"[{visemes[i]} {visemeState[y][i]} hold]";
                            if (offset != 17) textEvent = textEvent.ToLower();
                            lipsyncTrack.Add(new TextEvent() { Text = textEvent, DeltaTime=timeVal });
                            timeVal = 0;
                        }
                    }
                    visemeState[y].CopyTo(prevFrame, 0);
                }
            }
            return new TrackChunk(lipsyncTrack);
        }

        // the main fxn we actually care about
        // TODO: change from void return val to a List of TrackChunks
        public static List<TrackChunk> GetMidiFromMilo(byte[] miloBytes, TempoMap tmap){
            // inflate milo bytes
            // get dictionary of files and filebytes from inflated milo
            var MiloTracks = new List<TrackChunk>();
            // if no milo bytes, return empty list
            if(miloBytes == null || miloBytes.Length == 0) return MiloTracks;

            var MiloFiles = ParseMiloForFiles(miloBytes);
            // with our list of files and filebytes from the milo, we must convert each file to a MIDI track
            foreach(var f in MiloFiles){
                switch(f.Key){
                    case "song.anim":
                        MiloTracks.Add(AnimToMidi(f.Value, tmap));
                        Debug.Log($"found VENUE track in the milo");
                        break;
                    case "song.lipsync":
                        MiloTracks.Add(LipsyncToMidi(f.Value, tmap, "LIPSYNC1"));
                        Debug.Log($"found LIPSYNC1 track in the milo");
                        break;
                    case "part2.lipsync":
                        MiloTracks.Add(LipsyncToMidi(f.Value, tmap, "LIPSYNC2"));
                        Debug.Log($"found LIPSYNC2 track in the milo");
                        break;
                    case "part3.lipsync":
                        MiloTracks.Add(LipsyncToMidi(f.Value, tmap, "LIPSYNC3"));
                        Debug.Log($"found LIPSYNC3 track in the milo");
                        break;
                    case "part4.lipsync":
                        MiloTracks.Add(LipsyncToMidi(f.Value, tmap, "LIPSYNC4"));
                        Debug.Log($"found LIPSYNC4 track in the milo");
                        break;
                    default: break;
                }
            }
            return MiloTracks;
        }
    }
}