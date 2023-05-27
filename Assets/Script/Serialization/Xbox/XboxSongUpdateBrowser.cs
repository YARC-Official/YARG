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
	public static class XboxSongUpdateBrowser {
        public static Dictionary<string, List<DataArray>> FetchSongUpdates(string update_folder){
			var dtaTree = new DataArray();
			var UpdateSongDict = new Dictionary<string, List<DataArray>>();

            // Attempt to read songs_updates.dta
			try {
				using var sr = new StreamReader(Path.Combine(update_folder, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs_updates.dta for `{update_folder}`.");
				Debug.LogException(e);
				return null;
			}

            // Read each shortname the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					string currentName = currentArray.Name;

					if(UpdateSongDict.TryGetValue(currentName, out var value)){
						UpdateSongDict[currentName].Add(currentArray);
					}
					else {
						UpdateSongDict.Add(currentName, new List<DataArray>() { currentArray });
					}

				} catch (Exception e) {
					Debug.Log($"Failed to get shortname, skipping...");
					Debug.LogException(e);
				}
			}

			// Debug.Log($"Song updates:");
			// foreach(var item in UpdateSongDict){
			// 	Debug.Log($"{item.Key} has update array count of {item.Value.Count}");
			// }

            return UpdateSongDict;
        }
    }
}