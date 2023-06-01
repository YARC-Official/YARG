using System.Collections.Generic;
using System.IO;
using XboxSTFS;

namespace YARG.Song {
	public class SongProUpgrade {
		private XboxSTFSFile conFile;
        public string UpgradeMidiPath { get; private set; }
        public int UpgradeMidiIndex { get; private set; }

		public SongProUpgrade(BinaryReader reader, List<XboxSTFSFile> conFiles) {
			UpgradeMidiIndex = reader.ReadInt32();
			if (UpgradeMidiIndex != -1) {
				string filename = reader.ReadString();
				foreach (var con in conFiles)
					if (con.Filename == filename) {
						conFile = con;
						break;
					}

				if (conFile == null)
					throw new ConMissingException();
			}
			else
				UpgradeMidiPath = reader.ReadString();
		}

		public void WriteToCache(BinaryWriter writer) {
			writer.Write(UpgradeMidiIndex);
			if (UpgradeMidiIndex != -1)
				writer.Write(conFile.Filename);
			else
				writer.Write(UpgradeMidiPath);
		}

		public SongProUpgrade(string folder, string shortName) {
			UpgradeMidiPath = Path.Combine(folder, $"{shortName}_plus.mid");
		}

		public SongProUpgrade(XboxSTFSFile conFile, string shortName) {
			this.conFile = conFile;
			UpgradeMidiIndex = conFile.GetFileIndex(Path.Combine("songs_upgrades", $"{shortName}_plus.mid"));
		}

		public byte[] GetUpgradeMidi(){
			return conFile == null ? File.ReadAllBytes(UpgradeMidiPath) : conFile.LoadSubFile(UpgradeMidiIndex);
        }
    }
}