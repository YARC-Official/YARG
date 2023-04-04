using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using YARG.Data;

namespace YARG.Serialization {
    public static class SongsDta {
		static SongsDta() {

        }
        public static List<SongInfo> ParseSongsDta(File srcfile) {
            try {
                List<SongInfo> songList = new List<SongInfo>();
                Encoding dtaEnc = Encoding.GetEncoding("IBM01047"); // HMX chose Latin-1 for Data Array, for some reason
                recurseReadDta(ReadAllText(srcfile, dtaEnc)); 
                return songList;
            } catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfile.folder}`.");
				Debug.LogException(e);
			}
        }
        private string recurseReadDta(string dtaFile) {
            
        }
    }
}