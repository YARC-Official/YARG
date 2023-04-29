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
		public static List<XboxSong> BrowseCON(string conName) {
            var songList = new List<XboxSong>();
			var dtaTree = new DataArray();

            // Attempt to read songs.dta
			// to do this, create/construct a new XboxSTFS
            // then extract the songs.dta and read it into dtaTree
            STFS thisCON = new STFS(conName);
            dtaTree = DTX.FromPlainTextBytes(thisCON.GetFile("songs/songs.dta"));

            Debug.Log($"this songs.dta found {dtaTree.Count} songs, listing them now...");
            for(int i = 0; i < dtaTree.Count; i++){
                var currentArray = (DataArray) dtaTree[i];
                Debug.Log($"current shortname: {currentArray[0].Name}");
            }

			return songList;
        }
    }
}