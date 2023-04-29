using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;

namespace YARG.Serialization {
	public static class XboxRawfileBrowser {
		public static List<XboxSong> BrowseFolder(string folder) {
			var songList = new List<XboxSong>();
			var dtaTree = new DataArray();

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

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					var currentSong = new XboxSong(folder, currentArray);

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
	}
}