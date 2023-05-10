using System.Collections.Generic;
using System.IO;
using YARG.Serialization;

namespace YARG.Song {
	public static class CacheHelpers {
		
		public static void WriteExtractedConData(BinaryWriter writer, ExtractedConSongEntry ExCONSong) {
			/*/
			 
			 MOGG data
			 
			 */

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
			
			/*/
			 
			 Image data
			 
			 */

			// Note: ImagePath can be an empty string if the song has no image
			writer.Write(ExCONSong.ImagePath);
		}

		public static void WriteConData(BinaryWriter writer, ConSongEntry CONSong) {

			// midi file size and memory offsets
			writer.Write(CONSong.MidiFileSize);
			writer.Write(CONSong.MidiFileMemBlockOffsets.Length);
			for(int i = 0; i < CONSong.MidiFileMemBlockOffsets.Length; i++){
				writer.Write(CONSong.MidiFileMemBlockOffsets[i]);
			}

			// mogg file size and memory offsets
			writer.Write(CONSong.MoggFileSize);
			writer.Write(CONSong.MoggFileMemBlockOffsets.Length);
			for(int i = 0; i < CONSong.MoggFileMemBlockOffsets.Length; i++){
				writer.Write(CONSong.MoggFileMemBlockOffsets[i]);
			}

			// image file size and memory offsets, if they exist
			writer.Write(CONSong.ImageFileSize);
			if(CONSong.ImageFileMemBlockOffsets == null) writer.Write(0);
			else{
				writer.Write(CONSong.ImageFileMemBlockOffsets.Length);
				for(int i = 0; i < CONSong.ImageFileMemBlockOffsets.Length; i++){
					writer.Write(CONSong.ImageFileMemBlockOffsets[i]);
				}
			}

		}

		public static void ReadExtractedConData(BinaryReader reader, ExtractedConSongEntry ExCONSong) {
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

			// Note: ImagePath can be an empty string if the song has no image
			ExCONSong.ImagePath = reader.ReadString();
		}

		public static void ReadConData(BinaryReader reader, ConSongEntry CONSong) {

			// midi file size and memory offsets
			CONSong.MidiFileSize = reader.ReadUInt32();
			uint midiOffsetsLength = reader.ReadUInt32();
			CONSong.MidiFileMemBlockOffsets = new uint[midiOffsetsLength];
			for(int i = 0; i < midiOffsetsLength; i++){
				CONSong.MidiFileMemBlockOffsets[i] = reader.ReadUInt32();
			}

			// mogg file size and memory offsets
			CONSong.MoggFileSize = reader.ReadUInt32();
			uint moggOffsetsLength = reader.ReadUInt32();
			CONSong.MoggFileMemBlockOffsets = new uint[moggOffsetsLength];
			for(int i = 0; i < moggOffsetsLength; i++){
				CONSong.MoggFileMemBlockOffsets[i] = reader.ReadUInt32();
			}

			// image file size and memory offsets, if they exist
			CONSong.ImageFileSize = reader.ReadUInt32();
			uint imgOffsetsLength = reader.ReadUInt32();
			if(imgOffsetsLength > 0){
				CONSong.ImageFileMemBlockOffsets = new uint[imgOffsetsLength];
				for(int i = 0; i < imgOffsetsLength; i++){
					CONSong.ImageFileMemBlockOffsets[i] = reader.ReadUInt32();
				}
			}

		}

	}
}