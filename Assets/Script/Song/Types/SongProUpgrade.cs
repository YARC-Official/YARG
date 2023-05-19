using System.Collections.Generic;
using System.IO;
using YARG.Serialization;

namespace YARG.Song {
	public class SongProUpgrade {
        public string ShortName { get; set; }
        public string UpgradeMidiPath { get; set; }

        // only used if an upgrade is contained inside a CON!
        public string CONFilePath { get; set; } 
        public uint UpgradeMidiFileSize { get; set; }
        public uint[] UpgradeMidiFileMemBlockOffsets { get; set; }

        public byte[] GetUpgradeMidi(){
            if(string.IsNullOrEmpty(CONFilePath)) return File.ReadAllBytes(UpgradeMidiPath);
            else return XboxCONInnerFileRetriever.RetrieveFile(CONFilePath, UpgradeMidiFileSize, UpgradeMidiFileMemBlockOffsets);
        }
    }
}