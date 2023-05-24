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
		public static List<ExtractedConSongEntry> BrowseFolder(string root_folder, string update_folder, Dictionary<string, List<DataArray>> updates, List<(SongProUpgrade, DataArray)> upgrades){
			var songList = new List<ExtractedConSongEntry>();
			string songs_folder = Path.Combine(root_folder, "songs");
			string songs_upgrades_folder = Path.Combine(root_folder, "songs_upgrades");

			DataArray dtaTree;
			// capture any extra upgrades local to this excon, if they exist
			string dtaPath = Path.Combine(songs_upgrades_folder, "upgrades.dta");
			if (File.Exists(dtaPath)) {
				dtaTree = DTX.FromPlainTextBytes(File.ReadAllBytes(dtaPath));

				for (int i = 0; i < dtaTree.Count; i++) {
					try {
						upgrades.Add(new(new SongProUpgrade(songs_upgrades_folder, dtaTree[i].Name), (DataArray)dtaTree[i]));
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			dtaPath = Path.Combine(songs_folder, "songs.dta");
			if (!File.Exists(dtaPath)) {
				Debug.LogError($"\"{dtaPath}\" does not exist.");
				return null;
			}

			
			try {
				dtaTree = DTX.FromPlainTextBytes(File.ReadAllBytes(dtaPath));
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{songs_folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					ExtractedConSongEntry currentSong = new(songs_folder, currentArray);

					// check if song has applicable updates and/or upgrades
					if (updates.TryGetValue(currentSong.ShortName, out var updateValue)) {
						foreach (var dtaUpdate in updateValue)
							currentSong.SetFromDTA(dtaUpdate);
						currentSong.Update(update_folder);
					}

					currentSong.Upgrade(upgrades);

					MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), updateValue);
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