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
		public static List<ExtractedConSongEntry> BrowseFolder(string folder, 
				string update_folder, Dictionary<string, List<DataArray>> update_dict, 
				string upgrade_folder, Dictionary<string, DataArray> upgrade_dict){
			var songList = new List<ExtractedConSongEntry>();
			var dtaTree = new DataArray();

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					// Parse songs.dta for song metadata
					var currentSong = XboxDTAParser.ParseFromDta(currentArray);

					// check if song has applicable updates and/or upgrades
					bool songCanBeUpdated = (update_dict.TryGetValue(currentSong.ShortName, out var val));
					bool songHasUpgrade = (upgrade_dict.TryGetValue(currentSong.ShortName, out var upgrade_dta));

					// if shortname was found in songs_updates.dta, update the metadata
					if(songCanBeUpdated)
						foreach(var dtaUpdate in update_dict[currentSong.ShortName])
							currentSong = XboxDTAParser.ParseFromDta(dtaUpdate, currentSong);

					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now:
					
					// capture base midi, and if an update midi was provided, capture that as well
					currentSong.NotesFile = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mid");
					if(songCanBeUpdated && currentSong.DiscUpdate){
						string updateMidiPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mid");
						if(File.Exists(updateMidiPath)) currentSong.UpdateMidiPath = updateMidiPath;
						else {
							Debug.LogError($"Couldn't update song {currentSong.ShortName} - update file {currentSong.UpdateMidiPath} not found!");
							currentSong.DiscUpdate = false; // to prevent breaking in-game if the user still tries to play the song
						}
					}

					// capture base mogg path, OR, if update mogg was found, capture that instead
					currentSong.MoggPath = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mogg");
					if(songCanBeUpdated){
						string updateMoggPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mogg");
						if(File.Exists(updateMoggPath)){
							currentSong.UsingUpdateMogg = true;
							currentSong.MoggPath = updateMoggPath;
						}
					}

					// capture base image (if one was provided), OR if update image was found, capture that instead
					string imgPath = Path.Combine(folder, currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
					if(currentSong.HasAlbumArt && File.Exists(imgPath))
						currentSong.ImagePath = imgPath;
					if(songCanBeUpdated){
						string imgUpdatePath = Path.Combine(update_folder, currentSong.ShortName, "gen", $"{currentSong.ShortName}_keep.png_xbox");
						if(currentSong.HasAlbumArt && currentSong.AlternatePath){
							if(File.Exists(imgUpdatePath)) currentSong.ImagePath = imgUpdatePath;
							else currentSong.AlternatePath = false;
						}
					}

					// Get song folder path for mid, mogg, png_xbox
					currentSong.Location = Path.Combine(folder, currentSong.Location);
					
					// Parse the mogg
					using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs);

					currentSong.MoggHeader = br.ReadInt32();
					currentSong.MoggAddressAudioOffset = br.ReadInt32();
					currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), val);

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