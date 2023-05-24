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
		public static List<ConSongEntry> BrowseCON(XboxSTFSFile conFile, string update_folder, Dictionary<string, List<DataArray>> updates, List<(SongProUpgrade, DataArray)> upgrades){
			DataArray dtaTree;
			byte[] dtaFile = conFile.LoadSubFile(Path.Combine("song_upgrades","upgrades.dta"));
			if (dtaFile.Length > 0) {
				dtaTree = DTX.FromPlainTextBytes(dtaFile);

				// Read each shortname the dta file lists
				for (int i = 0; i < dtaTree.Count; i++) {
					try {
						upgrades.Add(new(new SongProUpgrade(conFile, dtaTree[i].Name), (DataArray)dtaTree[i]));
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			dtaFile = conFile.LoadSubFile(Path.Combine("songs", "songs.dta"));
			if (dtaFile.Length == 0) {
				Debug.Log("DTA file was not located in CON");
				return null;
			}

			// Attempt to read songs.dta
			try {
				dtaTree = DTX.FromPlainTextBytes(dtaFile);
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{conFile.Filename}`.");
				Debug.LogException(e);
				return null;
			}

			var songList = new List<ConSongEntry>();
			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					ConSongEntry currentSong = new(conFile, currentArray);

					// check if song has applicable updates and/or upgrades
					if (updates.TryGetValue(currentSong.ShortName, out var updateValue)) {
						foreach (var dtaUpdate in updateValue)
							currentSong.SetFromDTA(dtaUpdate);
						currentSong.Update(update_folder);
					}

					currentSong.Upgrade(upgrades);

					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), updateValue);

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