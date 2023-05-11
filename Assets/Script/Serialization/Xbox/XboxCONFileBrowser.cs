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
		public static List<ConSongEntry> BrowseCON(string conName, string update_folder, List<string> update_shortnames){
			var songList = new List<ConSongEntry>();
			var dtaTree = new DataArray();
			var dtaUpdateTree = new DataArray();

			// Attempt to read songs.dta
			STFS theCON = new STFS(conName);
			try {
				dtaTree = DTX.FromPlainTextBytes(theCON.GetFile(Path.Combine("songs", "songs.dta")));
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{conName}`.");
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
					ConSongEntry currentSong = XboxDTAParser.ParseFromDta(currentArray);

					// check if song has applicable updates
					bool songCanBeUpdated = (!String.IsNullOrEmpty(update_folder) && (update_shortnames.Find(s => s == currentSong.ShortName) != null));

					// if shortname was found in songs_updates.dta, update the metadata
					if(songCanBeUpdated)
						currentSong = XboxDTAParser.ParseFromDta(dtaUpdateTree.Array(currentSong.ShortName), currentSong);

					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now
					// since we're dealing with a CON and not an ExCON, grab each relevant file's sizes and memory block offsets
					
					// capture base midi, and if an update midi was provided, capture that as well
					currentSong.NotesFile = Path.Combine("songs", currentSong.Location, $"{currentSong.Location}.mid");
					currentSong.MidiFileSize = theCON.GetFileSize(currentSong.NotesFile);
					currentSong.MidiFileMemBlockOffsets = theCON.GetMemOffsets(currentSong.NotesFile);
					if(songCanBeUpdated && currentSong.DiscUpdate){
						string updateMidiPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mid");
						if(File.Exists(updateMidiPath)) currentSong.UpdateMidiPath = updateMidiPath;
						else {
							Debug.LogError($"Couldn't update song {currentSong.ShortName} - update file {currentSong.UpdateMidiPath} not found!");
							currentSong.DiscUpdate = false; // to prevent breaking in-game if the user still tries to play the song
						}
					}

					// capture base mogg path, OR, if update mogg was found, capture that instead
					if(songCanBeUpdated){
						string updateMoggPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mogg");
						if(File.Exists(updateMoggPath)){
							currentSong.UsingUpdateMogg = true;
							currentSong.MoggPath = updateMoggPath;
						}
					}
					if(!currentSong.UsingUpdateMogg){
						currentSong.MoggPath = Path.Combine("songs", currentSong.Location, $"{currentSong.Location}.mogg");
						currentSong.MoggFileSize = theCON.GetFileSize(currentSong.MoggPath);
						currentSong.MoggFileMemBlockOffsets = theCON.GetMemOffsets(currentSong.MoggPath);
					}
					
					// capture base image (if one was provided), OR if update image was found, capture that instead
					string imgPath = Path.Combine("songs", currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
					currentSong.ImageFileSize = theCON.GetFileSize(imgPath);
					currentSong.ImageFileMemBlockOffsets = theCON.GetMemOffsets(imgPath);
					if(currentSong.HasAlbumArt && currentSong.ImageFileSize > 0 && currentSong.ImageFileMemBlockOffsets != null)
						currentSong.ImagePath = imgPath;
					if(songCanBeUpdated){
						string imgUpdatePath = Path.Combine(update_folder, currentSong.ShortName, "gen", $"{currentSong.ShortName}_keep.png_xbox");
						if(currentSong.HasAlbumArt && currentSong.AlternatePath){
							if(File.Exists(imgUpdatePath)) currentSong.ImagePath = imgUpdatePath;
							else currentSong.AlternatePath = false;
						}
					}

					// Set this song's "Location" to the path of the CON file
					currentSong.Location = conName;
					
					// Parse the mogg
					if(!currentSong.UsingUpdateMogg){
						using var fs = new FileStream(conName, FileMode.Open, FileAccess.Read);
						using var br = new BinaryReader(fs);
						fs.Seek(currentSong.MoggFileMemBlockOffsets[0], SeekOrigin.Begin);

						currentSong.MoggHeader = br.ReadInt32();
						currentSong.MoggAddressAudioOffset = br.ReadInt32();
						currentSong.MoggAudioLength = currentSong.MoggFileSize - currentSong.MoggAddressAudioOffset;
					}
					else{
						using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
						using var br = new BinaryReader(fs);

						currentSong.MoggHeader = br.ReadInt32();
						currentSong.MoggAddressAudioOffset = br.ReadInt32();
						currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					}
					
					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), dtaUpdateTree.Array(currentSong.ShortName));

					// Debug.Log($"{currentSong.ShortName}:\nMidi path: {currentSong.NotesFile}\nMogg path: {currentSong.MoggPath}\nImage path: {currentSong.ImagePath}");

					// will validate the song outside of this class, in SongScanThread.cs
					// so okay to add to song list for now
					songList.Add(currentSong);
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}
	}
}