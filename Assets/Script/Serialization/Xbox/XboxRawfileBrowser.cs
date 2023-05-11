using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public static class ExCONBrowser {
		public static List<ExtractedConSongEntry> BrowseFolder(string folder, string update_folder, List<string> update_shortnames){
			var songList = new List<ExtractedConSongEntry>();
			var dtaTree = new DataArray();
			var dtaUpdateTree = new DataArray();

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Attempt to read songs_updates.dta, if it exists
			if(update_folder != string.Empty){
				try {
					using var sr_upd = new StreamReader(Path.Combine(update_folder, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
					dtaUpdateTree = DTX.FromDtaString(sr_upd.ReadToEnd());
				} catch (Exception e_upd) {
					Debug.LogError($"Failed to parse songs_updates.dta for `{update_folder}`.");
					Debug.LogException(e_upd);
					return null;
				}
			}

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					// Parse songs.dta
					// Get song metadata from songs.dta
					var currentSong = XboxDTAParser.ParseFromDta(currentArray);

					// if shortname was found in songs_updates.dta, update the metadata
					if(update_folder != string.Empty){
						if(update_shortnames.Find(s => s == currentSong.ShortName) != null){
							Debug.Log($"updating metadata for {currentSong.ShortName}");
							currentSong = XboxDTAParser.ParseFromDta(dtaUpdateTree.Array(currentSong.ShortName), currentSong);
							Debug.Log($"{currentSong.ShortName} updated.");
						}
					}
					
					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now
					currentSong.NotesFile = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mid");
					currentSong.MoggPath = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mogg");
					string imgPath = Path.Combine(folder, currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
					if(currentSong.HasAlbumArt && File.Exists(imgPath))
						currentSong.ImagePath = imgPath;

					// Get song folder path for mid, mogg, png_xbox
					currentSong.Location = Path.Combine(folder, currentSong.Location);
					
					// Parse the mogg
					using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs);

					currentSong.MoggHeader = br.ReadInt32();
					currentSong.MoggAddressAudioOffset = br.ReadInt32();
					currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"));

					// Debug.Log($"{currentSong.ShortName}:\nMidi path: {currentSong.NotesFile}\nMogg path: {currentSong.MoggPath}\nImage path: {currentSong.ImagePath}");

					// will validate the song outside of this class, in SongScanThread.cs
					// so okay to add to song list for now
					songList.Add((ExtractedConSongEntry)currentSong);
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}
	}
}