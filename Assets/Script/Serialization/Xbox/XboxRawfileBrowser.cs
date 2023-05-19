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
		public static List<ExtractedConSongEntry> BrowseFolder(string root_folder, 
				string update_folder, Dictionary<string, List<DataArray>> update_dict, 
				Dictionary<SongProUpgrade, DataArray> upgrade_dict){
			var songList = new List<ExtractedConSongEntry>();
			var dtaTree = new DataArray();
			string songs_folder = Path.Combine(root_folder, "songs");
			string songs_upgrades_folder = Path.Combine(root_folder, "songs_upgrades"); // TODO: implement this

			// capture any extra upgrades local to this excon, if they exist
			if(File.Exists(Path.Combine(songs_upgrades_folder, "upgrades.dta"))){
				using var sr_upgr = new StreamReader(Path.Combine(songs_upgrades_folder, "upgrades.dta"), Encoding.GetEncoding("iso-8859-1"));
				var dtaUpgradeTree = DTX.FromDtaString(sr_upgr.ReadToEnd());

				// Read each shortname the dta file lists
				for (int i = 0; i < dtaUpgradeTree.Count; i++) {
					try {
						var currentArray = (DataArray) dtaUpgradeTree[i];
						var upgr = new SongProUpgrade();
						upgr.ShortName = currentArray.Name;
						upgr.UpgradeMidiPath = Path.Combine(songs_upgrades_folder, $"{currentArray.Name}_plus.mid");
						upgrade_dict.Add(upgr, currentArray);
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(songs_folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{songs_folder}`.");
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

					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now:
					
					// capture base midi, and if an update midi was provided, capture that as well
					currentSong.NotesFile = Path.Combine(songs_folder, currentSong.Location, $"{currentSong.Location}.mid");
					if(songCanBeUpdated && currentSong.DiscUpdate){
						string updateMidiPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mid");
						if(File.Exists(updateMidiPath)) currentSong.UpdateMidiPath = updateMidiPath;
						else {
							Debug.LogError($"Couldn't update song {currentSong.ShortName} - update file {currentSong.UpdateMidiPath} not found!");
							currentSong.DiscUpdate = false; // to prevent breaking in-game if the user still tries to play the song
						}
					}

					// capture base mogg path, OR, if update mogg was found, capture that instead
					currentSong.MoggPath = Path.Combine(songs_folder, currentSong.Location, $"{currentSong.Location}.mogg");
					if(songCanBeUpdated){
						string updateMoggPath = Path.Combine(update_folder, currentSong.ShortName, $"{currentSong.ShortName}_update.mogg");
						if(File.Exists(updateMoggPath)){
							currentSong.UsingUpdateMogg = true;
							currentSong.MoggPath = updateMoggPath;
						}
					}

					// capture base image (if one was provided), OR if update image was found, capture that instead
					string imgPath = Path.Combine(songs_folder, currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");
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
					currentSong.Location = Path.Combine(songs_folder, currentSong.Location);
					
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