using System.Collections.Generic;
using System.IO;
using XboxSTFS;
using YARG.Serialization;

namespace YARG.Song {
	public static class CacheHelpers {
		
		public static void WriteExtractedConData(BinaryWriter writer, ExtractedConSongEntry ExCONSong) {

			// update midi data
			writer.Write(ExCONSong.DiscUpdate);
			writer.Write(ExCONSong.UpdateMidiPath);

			// pro upgrade data
			writer.Write(ExCONSong.SongUpgrade.ShortName);
			writer.Write(ExCONSong.SongUpgrade.UpgradeMidiPath);
			writer.Write(ExCONSong.SongUpgrade.CONFilePath);
			if(ExCONSong.SongUpgrade.UpgradeFL == null) writer.Write(false);
			else {
				writer.Write(true);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.filename);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.flags);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.numBlocks);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.firstBlock);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.size);
				writer.Write(ExCONSong.SongUpgrade.UpgradeFL.pathIndex);
			}

			if(ExCONSong.RealGuitarTuning != null){
				writer.Write(ExCONSong.RealGuitarTuning.Length);
				for(int i = 0; i < 6; i++)
					writer.Write(ExCONSong.RealGuitarTuning[i]);
			}
			else writer.Write(0);
			if(ExCONSong.RealBassTuning != null){
				writer.Write(ExCONSong.RealBassTuning.Length);
				for(int i = 0; i < 4; i++)
					writer.Write(ExCONSong.RealBassTuning[i]);
			}
			else writer.Write(0);

			// mogg data
			writer.Write(ExCONSong.UsingUpdateMogg);
			writer.Write(ExCONSong.MoggPath);
			writer.Write(ExCONSong.MoggHeader);
			writer.Write(ExCONSong.MoggAddressAudioOffset);
			writer.Write(ExCONSong.MoggAudioLength);

			// Write Stem Data
			writer.Write(ExCONSong.StemMaps.Count);
			foreach(var stem in ExCONSong.StemMaps){
				writer.Write((int)stem.Key);
				writer.Write(stem.Value.Length);
				foreach (int i in stem.Value) {
					writer.Write(i);
				}
			}

			// Write Matrix Data
			writer.Write(ExCONSong.MatrixRatios.GetLength(0));
			writer.Write(ExCONSong.MatrixRatios.GetLength(1));
			for (int i = 0; i < ExCONSong.MatrixRatios.GetLength(0); i++) {
				for (int j = 0; j < ExCONSong.MatrixRatios.GetLength(1); j++) {
					writer.Write(ExCONSong.MatrixRatios[i, j]);
				}
			}
			
			// image data
			writer.Write(ExCONSong.AlternatePath);
			// Note: ImagePath can be an empty string if the song has no image
			writer.Write(ExCONSong.ImagePath);
		}

		public static void WriteConData(BinaryWriter writer, ConSongEntry CONSong) {

			if(CONSong.FLMidi == null) writer.Write(false);
			else{
				writer.Write(true);
				writer.Write(CONSong.FLMidi.filename);
				writer.Write(CONSong.FLMidi.flags);
				writer.Write(CONSong.FLMidi.numBlocks);
				writer.Write(CONSong.FLMidi.firstBlock);
				writer.Write(CONSong.FLMidi.size);
				writer.Write(CONSong.FLMidi.pathIndex);
			}

			if(CONSong.FLMogg == null) writer.Write(false);
			else{
				writer.Write(true);
				writer.Write(CONSong.FLMogg.filename);
				writer.Write(CONSong.FLMogg.flags);
				writer.Write(CONSong.FLMogg.numBlocks);
				writer.Write(CONSong.FLMogg.firstBlock);
				writer.Write(CONSong.FLMogg.size);
				writer.Write(CONSong.FLMogg.pathIndex);
			}

			if(CONSong.FLImg == null) writer.Write(false);
			else{
				writer.Write(true);
				writer.Write(CONSong.FLImg.filename);
				writer.Write(CONSong.FLImg.flags);
				writer.Write(CONSong.FLImg.numBlocks);
				writer.Write(CONSong.FLImg.firstBlock);
				writer.Write(CONSong.FLImg.size);
				writer.Write(CONSong.FLImg.pathIndex);
			}

		}

		public static void ReadExtractedConData(BinaryReader reader, ExtractedConSongEntry ExCONSong) {

			// update midi data
			ExCONSong.DiscUpdate = reader.ReadBoolean();
			ExCONSong.UpdateMidiPath = reader.ReadString();

			// pro upgrade data
			SongProUpgrade upgr = new SongProUpgrade();
			upgr.ShortName = reader.ReadString();
			upgr.UpgradeMidiPath = reader.ReadString();
			upgr.CONFilePath = reader.ReadString();
			FileListing upgrFL = new FileListing();
			bool FLExists = reader.ReadBoolean();
			if(FLExists){
				upgrFL.filename = reader.ReadString();
				upgrFL.flags = reader.ReadByte();
				upgrFL.numBlocks = reader.ReadUInt32();
				upgrFL.firstBlock = reader.ReadUInt32();
				upgrFL.size = reader.ReadUInt32();
				upgrFL.pathIndex = reader.ReadInt16();
			}
			upgr.UpgradeFL = upgrFL;
			ExCONSong.SongUpgrade = upgr;

			int guitarTuneLength = reader.ReadInt32();
			if(guitarTuneLength > 0){
				ExCONSong.RealGuitarTuning = new int[guitarTuneLength];
				for(int i = 0; i < guitarTuneLength; i++)
					ExCONSong.RealGuitarTuning[i] = reader.ReadInt32();
			}
			int bassTuneLength = reader.ReadInt32();
			if(bassTuneLength > 0){
				ExCONSong.RealBassTuning = new int[bassTuneLength];
				for(int i = 0; i < bassTuneLength; i++)
					ExCONSong.RealBassTuning[i] = reader.ReadInt32();
			}

			// mogg data
			ExCONSong.UsingUpdateMogg = reader.ReadBoolean();
			ExCONSong.MoggPath = reader.ReadString();
			ExCONSong.MoggHeader = reader.ReadInt32();
			ExCONSong.MoggAddressAudioOffset = reader.ReadInt32();
			ExCONSong.MoggAudioLength = reader.ReadInt64();
			
			// Read Stem Data
			int stemCount = reader.ReadInt32();
			var stemMaps = new Dictionary<SongStem, int[]>();
			for (int i = 0; i < stemCount; i++) {
				var stem = (SongStem)reader.ReadInt32();
				int stemLength = reader.ReadInt32();
				var stemMap = new int[stemLength];
				for (int j = 0; j < stemLength; j++) {
					stemMap[j] = reader.ReadInt32();
				}
				stemMaps.Add(stem, stemMap);
			}

			ExCONSong.StemMaps = stemMaps;
			
			// Read Matrix Data
			int matrixRowCount = reader.ReadInt32();
			int matrixColCount = reader.ReadInt32();
			var matrixRatios = new float[matrixRowCount, matrixColCount];
			for (int i = 0; i < matrixRowCount; i++) {
				for (int j = 0; j < matrixColCount; j++) {
					matrixRatios[i, j] = reader.ReadSingle();
				}
			}

			ExCONSong.MatrixRatios = matrixRatios;

			/*/
			 
			 Image data
			 
			 */

			ExCONSong.AlternatePath = reader.ReadBoolean();
			// Note: ImagePath can be an empty string if the song has no image
			ExCONSong.ImagePath = reader.ReadString();
		}

		public static void ReadConData(BinaryReader reader, ConSongEntry CONSong) {

			FileListing midFL = new FileListing();
			bool midiFLExists = reader.ReadBoolean();
			if(midiFLExists){
				midFL.filename = reader.ReadString();
				midFL.flags = reader.ReadByte();
				midFL.numBlocks = reader.ReadUInt32();
				midFL.firstBlock = reader.ReadUInt32();
				midFL.size = reader.ReadUInt32();
				midFL.pathIndex = reader.ReadInt16();
			}
			CONSong.FLMidi = midFL;

			FileListing moggFL = new FileListing();
			bool moggFLExists = reader.ReadBoolean();
			if(moggFLExists){
				moggFL.filename = reader.ReadString();
				moggFL.flags = reader.ReadByte();
				moggFL.numBlocks = reader.ReadUInt32();
				moggFL.firstBlock = reader.ReadUInt32();
				moggFL.size = reader.ReadUInt32();
				moggFL.pathIndex = reader.ReadInt16();
			}
			CONSong.FLMogg = moggFL;
			
			FileListing imgFL = new FileListing();
			bool imgFLExists = reader.ReadBoolean();
			if(imgFLExists){
				imgFL.filename = reader.ReadString();
				imgFL.flags = reader.ReadByte();
				imgFL.numBlocks = reader.ReadUInt32();
				imgFL.firstBlock = reader.ReadUInt32();
				imgFL.size = reader.ReadUInt32();
				imgFL.pathIndex = reader.ReadInt16();
			}
			CONSong.FLImg = imgFL;

		}

	}
}