using System.Collections.Generic;
using System.IO;
using YARG.Serialization;

namespace YARG.Song {
	public static class CacheHelpers {
		
		public static void WriteExtractedConData(BinaryWriter writer, ExtractedConSongEntry conSong) {
			/*/
			 
			 MOGG data
			 
			 */
			
			writer.Write(conSong.MoggInfo.MoggPath);
			writer.Write(conSong.MoggInfo.ChannelCount);
			writer.Write(conSong.MoggInfo.Header);
			writer.Write(conSong.MoggInfo.MoggAddressAudioOffset);
			writer.Write(conSong.MoggInfo.MoggAudioLength);
			
			// Write Pan Data
			writer.Write(conSong.MoggInfo.PanData.Length);
			foreach (float pan in conSong.MoggInfo.PanData) {
				writer.Write(pan);
			}
			
			// Write Volume Data
			writer.Write(conSong.MoggInfo.VolumeData.Length);
			foreach (float vol in conSong.MoggInfo.VolumeData) {
				writer.Write(vol);
			}
			
			// Write Track Data
			writer.Write(conSong.MoggInfo.Tracks.Count);
			foreach (var track in conSong.MoggInfo.Tracks) {
				writer.Write(track.Key);
				writer.Write(track.Value.Length);
				foreach (int i in track.Value) {
					writer.Write(i);
				}
			}
			
			// Write Crowd Data
			writer.Write(conSong.MoggInfo.CrowdChannels.Length);
			foreach (int i in conSong.MoggInfo.CrowdChannels) {
				writer.Write(i);
			}
			
			// Write Stem Data
			writer.Write(conSong.MoggInfo.StemMaps.Count);
			foreach (var stem in conSong.MoggInfo.StemMaps) {
				writer.Write((int)stem.Key);
				writer.Write(stem.Value.Length);
				foreach (int i in stem.Value) {
					writer.Write(i);
				}
			}
			
			// Write Matrix Data
			writer.Write(conSong.MoggInfo.MatrixRatios.GetLength(0));
			writer.Write(conSong.MoggInfo.MatrixRatios.GetLength(1));
			for (int i = 0; i < conSong.MoggInfo.MatrixRatios.GetLength(0); i++) {
				for (int j = 0; j < conSong.MoggInfo.MatrixRatios.GetLength(1); j++) {
					writer.Write(conSong.MoggInfo.MatrixRatios[i, j]);
				}
			}
			
			/*/
			 
			 Image data
			 
			 */

			// ImageInfo can be null if the song has no image so need to detect this
			writer.Write(conSong.ImageInfo is not null);
			if (conSong.ImageInfo is not null) {
				writer.Write(conSong.ImageInfo.ImagePath);
				writer.Write(conSong.ImageInfo.BitsPerPixel);
				writer.Write(conSong.ImageInfo.Format);
			}
		}

		public static void ReadExtractedConData(BinaryReader reader, ExtractedConSongEntry conSong) {
			string path = reader.ReadString();
			int channelCount = reader.ReadInt32();
			int header = reader.ReadInt32();
			int moggAddressAudioOffset = reader.ReadInt32();
			long moggAudioLength = reader.ReadInt64();
			
			// Read Pan Data
			int panDataLength = reader.ReadInt32();
			var panData = new float[panDataLength];
			for (int i = 0; i < panDataLength; i++) {
				panData[i] = reader.ReadSingle();
			}
			
			// Read Volume Data
			int volumeDataLength = reader.ReadInt32();
			var volumeData = new float[volumeDataLength];
			for (int i = 0; i < volumeDataLength; i++) {
				volumeData[i] = reader.ReadSingle();
			}
			
			// Read Track Data
			int trackCount = reader.ReadInt32();
			var tracks = new Dictionary<string, int[]>();
			for (int i = 0; i < trackCount; i++) {
				string trackName = reader.ReadString();
				int trackLength = reader.ReadInt32();
				var track = new int[trackLength];
				for (int j = 0; j < trackLength; j++) {
					track[j] = reader.ReadInt32();
				}
				tracks.Add(trackName, track);
			}
			
			// Read Crowd Data
			int crowdChannelCount = reader.ReadInt32();
			var crowdChannels = new int[crowdChannelCount];
			for (int i = 0; i < crowdChannelCount; i++) {
				crowdChannels[i] = reader.ReadInt32();
			}
			
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
			
			// Read Matrix Data
			int matrixRowCount = reader.ReadInt32();
			int matrixColCount = reader.ReadInt32();
			var matrixRatios = new float[matrixRowCount, matrixColCount];
			for (int i = 0; i < matrixRowCount; i++) {
				for (int j = 0; j < matrixColCount; j++) {
					matrixRatios[i, j] = reader.ReadSingle();
				}
			}

			var moggData = new XboxMoggData(path) {
				ChannelCount = channelCount,
				Header = header,
				MoggAddressAudioOffset = moggAddressAudioOffset,
				MoggAudioLength = moggAudioLength,
				PanData = panData,
				VolumeData = volumeData,
				Tracks = tracks,
				CrowdChannels = crowdChannels,
				StemMaps = stemMaps,
				MatrixRatios = matrixRatios
			};

			/*/
			 
			 Image data
			 
			 */

			// ImageInfo can be null if the song has no image so need to detect this
			if (reader.ReadBoolean()) {
				string imagePath = reader.ReadString();
				byte bitsPerPixel = reader.ReadByte();
				int format = reader.ReadInt32();
				
				var imageInfo = new XboxImage(imagePath) {
					BitsPerPixel = bitsPerPixel,
					Format = format
				};
				
				conSong.ImageInfo = imageInfo;
			}

			conSong.MoggInfo = moggData;
		}

	}
}