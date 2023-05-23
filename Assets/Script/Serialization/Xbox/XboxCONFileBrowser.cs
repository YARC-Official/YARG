using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using XboxSTFS;
using static XboxSTFS.XboxSTFSParser;
using YARG.Song;

namespace YARG.Serialization {
	public static class XboxCONFileBrowser {
		public static List<ConSongEntry> BrowseCON(string conName, 
				string update_folder, Dictionary<string, List<DataArray>> update_dict,
				Dictionary<SongProUpgrade, DataArray> upgrade_dict){
			var songList = new List<ConSongEntry>();
			var dtaTree = new DataArray();
			var CONFileListings = XboxSTFSParser.GetCONFileListings(conName);

			// Attempt to read upgrades.dta, if it exists
			if(CONFileListings.TryGetValue(Path.Combine("songs_upgrades", "upgrades.dta"), out var UpgradeFL)){
				var dtaUpgradeTree = DTX.FromPlainTextBytes(XboxSTFSParser.GetFile(conName, UpgradeFL));

				// Read each shortname the dta file lists
				for (int i = 0; i < dtaUpgradeTree.Count; i++) {
					try {
						var currentArray = (DataArray) dtaUpgradeTree[i];
						var upgr = new SongProUpgrade();
						upgr.ShortName = currentArray.Name;
						upgr.UpgradeMidiPath = Path.Combine("songs_upgrades", $"{currentArray.Name}_plus.mid");
						upgr.CONFilePath = conName;
						upgr.UpgradeFL = UpgradeFL;
						upgrade_dict.Add(upgr, currentArray);
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			// Attempt to read songs.dta
			try {
				dtaTree = DTX.FromPlainTextBytes(XboxSTFSParser.GetFile(conName, CONFileListings[Path.Combine("songs", "songs.dta")]));
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

					// check if song has applicable updates and/or upgrades
					bool songCanBeUpdated = (update_dict.TryGetValue(currentSong.ShortName, out var val));
					bool songHasUpgrade = false;
					foreach(var upgr in upgrade_dict){
						if(upgr.Key.ShortName == currentSong.ShortName){
							songHasUpgrade = true;
							currentSong.SongUpgrade = upgr.Key;
							break;
						}
					}

					// if shortname was found in songs_updates.dta, update the metadata
					if(songCanBeUpdated)
						foreach(var dtaUpdate in update_dict[currentSong.ShortName])
							currentSong = XboxDTAParser.ParseFromDta(dtaUpdate, currentSong);

					// if shortname was found in upgrades.dta, apply the upgrade metadata (upgrade midi has already been captured)
					if(songHasUpgrade) currentSong = XboxDTAParser.ParseFromDta(upgrade_dict[currentSong.SongUpgrade], currentSong);

					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now
					// since we're dealing with a CON and not an ExCON, grab each relevant file's sizes and memory block offsets
					
					// capture base midi, and if an update midi was provided, capture that as well
					currentSong.NotesFile = Path.Combine("songs", currentSong.Location, $"{currentSong.Location}.mid");
					currentSong.FLMidi = CONFileListings[currentSong.NotesFile];
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
						currentSong.FLMogg = CONFileListings[currentSong.MoggPath];
					}
					
					// capture base image (if one was provided), OR if update image was found, capture that instead
					string imgPath = Path.Combine("songs", currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
					if(CONFileListings.TryGetValue(imgPath, out var imgVal)) currentSong.FLImg = imgVal;
					if(currentSong.HasAlbumArt && imgVal != null) currentSong.ImagePath = imgPath;
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
						var MoggBytes = XboxSTFSParser.GetMoggHeader(conName, currentSong.FLMogg);

						currentSong.MoggHeader = BitConverter.ToInt32(MoggBytes, 0);
						currentSong.MoggAddressAudioOffset = BitConverter.ToInt32(MoggBytes, 4);
						currentSong.MoggAudioLength = currentSong.FLMogg.size - currentSong.MoggAddressAudioOffset;
					}
					else{
						using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
						using var br = new BinaryReader(fs);

						currentSong.MoggHeader = br.ReadInt32();
						currentSong.MoggAddressAudioOffset = br.ReadInt32();
						currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					}
					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), val);

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