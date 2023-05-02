using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;

namespace YARG.Serialization {
	public static class XboxRawfileBrowser {
		public static List<XboxSong> BrowseFolder(string folder, string folder_update) {
			var songList = new List<XboxSong>();
			var dtaTree = new DataArray();
			var dtaUpdate = new DataArray();

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());

				Debug.Log("Successfully read dta");
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Attempt to read songs_updates.dta, if update folder was provided
			// if (folder_update != null) {
			// 	try {
			// 		using var sr = new StreamReader(Path.Combine(folder_update, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
			// 		dtaUpdate = DTX.FromDtaString(sr.ReadToEnd());

			// 		Debug.Log("Successfully read update dta");
			// 	} catch (Exception ee) {
			// 		Debug.LogError($"Failed to parse songs_updates.dta for `{folder_update}`.");
			// 		Debug.LogException(ee);
			// 		return null;
			// 	}
			// }

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					var currentSong = new XboxSong(folder, currentArray);

					// if updates were provided
					// if (folder_update != null) {
					// 	// if dtaUpdate has the matching shortname, update that XboxSong
					// 	if (dtaUpdate.Array(currentSong.ShortName) is DataArray dtaMissing) {
					// 		currentSong.UpdateSong(folder_update, dtaMissing);
					// 	}
					// }

					if (currentSong.IsValidSong()) {
						songList.Add(currentSong);
					} else {
						Debug.LogError($"Song with shortname `{currentSong.ShortName}` is invalid. Skipping.");
					}
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}

		// public static void BrowseUpdateFolder(string folder, List<XboxSong> baseSongs) {
		// 	var dtaTree = new DataArray();

		// 	// Attempt to read songs_updates.dta
		// 	try {
		// 		using var sr = new StreamReader(Path.Combine(folder, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
		// 		dtaTree = DTX.FromDtaString(sr.ReadToEnd());

		// 		Debug.Log("Successfully read update dta");
		// 	} catch (Exception e) {
		// 		Debug.LogError($"Failed to parse songs_updates.dta for `{folder}`.");
		// 		Debug.LogException(e);
		// 		return;
		// 	}

		// 	// Read each song the update dta lists
		// 	for (int i = 0; i < dtaTree.Count; i++) {
		// 		Debug.Log(dtaTree[i].Name);
		// 		// if(baseSongs.ShortName)
		// 	}
		// }
	}
}