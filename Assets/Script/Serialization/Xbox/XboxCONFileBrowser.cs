using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using XboxSTFS;

namespace YARG.Serialization {
    public static class XboxCONFileBrowser {
		public static List<XboxCONSong> BrowseCON(string conName) {
            var songList = new List<XboxCONSong>();
			var dtaTree = new DataArray();

            // Attempt to read songs.dta
            STFS thisCON = new STFS(conName);
            try{
                dtaTree = DTX.FromPlainTextBytes(thisCON.GetFile("songs/songs.dta"));
                Debug.Log("Successfully read dta");
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{conName}`.");
				Debug.LogException(e);
				return null;
			}
            
            // Read each song the dta file lists
            for(int i = 0; i < dtaTree.Count; i++){
                try {
					var currentArray = (DataArray) dtaTree[i];
                    var currentSong = new XboxCONSong(conName, currentArray, thisCON);
                    currentSong.ParseSong();

					if (currentSong.IsValidSong()) {
						songList.Add(currentSong);
					} else {
						Debug.LogError($"Song with shortname `{currentSong.shortname}` is invalid. Skipping.");
					}
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
            }

            // XboxCONSong lol = new XboxCONSong(conName, (DataArray)dtaTree[dtaTree.Count - 1], thisCON);
            // lol.GetMoggFile();

			return songList;
        }
    }
}