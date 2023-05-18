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
	public static class XboxSongUpgradeBrowser {
        public static Dictionary<string, DataArray> FetchSongUpgrades(string upgrade_folder){
            var dtaTree = new DataArray();
			var UpgradeSongDict = new Dictionary<string, DataArray>();

            // Attempt to read upgrades.dta
			try {
				using var sr = new StreamReader(Path.Combine(upgrade_folder, "upgrades.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse upgrades.dta for `{upgrade_folder}`.");
				Debug.LogException(e);
				return null;
			}

            // Read each shortname the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
                    UpgradeSongDict.Add(currentArray.Name, currentArray);
				} catch (Exception e) {
					Debug.Log($"Failed to get upgrade, skipping...");
					Debug.LogException(e);
				}
			}

            // Debug.Log($"Song upgrades:");
			// foreach(var item in UpgradeSongDict){
			// 	Debug.Log($"{item.Key} has a pro upgrade");
			// }

            return UpgradeSongDict;

        }
    }
}