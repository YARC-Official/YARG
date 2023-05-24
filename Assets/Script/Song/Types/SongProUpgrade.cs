using System.Collections.Generic;
using System.IO;
using XboxSTFS;
using static XboxSTFS.XboxSTFSParser;
using YARG.Serialization;

namespace YARG.Song {
	public class SongProUpgrade {
        public string ShortName { get; set; }
        public string UpgradeMidiPath { get; set; }

        // only used if an upgrade is contained inside a CON!
        public string CONFilePath { get; set; } 
        public FileListing UpgradeFL { get; set; }

        public byte[] GetUpgradeMidi(){
            if(string.IsNullOrEmpty(CONFilePath)) return File.ReadAllBytes(UpgradeMidiPath);
            else return XboxSTFSParser.GetFile(CONFilePath, UpgradeFL);
        }
    }
}