using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using XboxSTFS;
using YARG.Song;

namespace YARG.Serialization {
	public static class XboxCONFileBrowser {
		public static List<ConSongEntry> BrowseCON(string conName){
			Debug.Log($"con name = {conName}");
			var songList = new List<ConSongEntry>();
			var dtaTree = new DataArray();

			// Attempt to read songs.dta
			STFS theCON = new STFS(conName);
			try {
				dtaTree = DTX.FromPlainTextBytes(theCON.GetFile("songs/songs.dta"));
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{conName}`.");
				Debug.LogException(e);
				return null;
			}

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					// Parse songs.dta
					// Get song metadata from songs.dta
					ConSongEntry currentSong = XboxDTAParser.ParseFromDta(currentArray);
					
					// TODO: alongside this stuff below, make sure you assign file size and mem block offsets!
					// // since Location is currently set to the name of the folder before mid/mogg/png, set those paths now
					// currentSong.NotesFile = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mid");
					// currentSong.MoggPath = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mogg");
					// string imgPath = Path.Combine(folder, currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
					// if(currentSong.HasAlbumArt && File.Exists(imgPath))
					// 	currentSong.ImagePath = imgPath;

					// // Get song folder path for mid, mogg, png_xbox
					// currentSong.Location = Path.Combine(folder, currentSong.Location);
					
					// // Parse the mogg
					// using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
					// using var br = new BinaryReader(fs);

					// currentSong.MoggHeader = br.ReadInt32();
					// currentSong.MoggAddressAudioOffset = br.ReadInt32();
					// currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					// MoggBASSInfoGenerator(currentSong, currentArray.Array("song"));

					// // Debug.Log($"{currentSong.ShortName}:\nMidi path: {currentSong.NotesFile}\nMogg path: {currentSong.MoggPath}\nImage path: {currentSong.ImagePath}");

					// // will validate the song outside of this class, in SongScanThread.cs
					// // so okay to add to song list for now
					// songList.Add(currentSong);
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}
	}
}

// namespace YARG.Serialization {
//     public static class XboxCONFileBrowser {
// 		public static List<XboxCONSong> BrowseCON(string conName) {
//             var songList = new List<XboxCONSong>();
// 			var dtaTree = new DataArray();

//             // Attempt to read songs.dta
//             STFS thisCON = new STFS(conName);
//             try{
//                 dtaTree = DTX.FromPlainTextBytes(thisCON.GetFile("songs/songs.dta"));
//                 Debug.Log("Successfully read dta");
// 			} catch (Exception e) {
// 				Debug.LogError($"Failed to parse songs.dta for `{conName}`.");
// 				Debug.LogException(e);
// 				return null;
// 			}
            
//             // Read each song the dta file lists
//             for(int i = 0; i < dtaTree.Count; i++){
//                 try {
// 					var currentArray = (DataArray) dtaTree[i];
//                     var currentSong = new XboxCONSong(conName, currentArray, thisCON);
//                     currentSong.ParseSong();

// 					if (currentSong.IsValidSong()) {
// 						songList.Add(currentSong);
// 					} else {
// 						Debug.LogError($"Song with shortname `{currentSong.shortname}` is invalid. Skipping.");
// 					}
// 				} catch (Exception e) {
// 					Debug.Log($"Failed to load song, skipping...");
// 					Debug.LogException(e);
// 				}
//             }

//             // XboxCONSong lol = new XboxCONSong(conName, (DataArray)dtaTree[dtaTree.Count - 1], thisCON);
//             // lol.GetMoggFile();

// 			return songList;
//         }
//     }
// }