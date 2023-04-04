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
        public static List<SongInfo> ParseSongsDta(DirectoryInfo srcfolder) {
            try {
                List<SongInfo> songList = new List<SongInfo>();
                Encoding dtaEnc = Encoding.GetEncoding("IBM01047"); // HMX chose Latin-1 for Data Array, for some reason
                string dtaFile;
                using (StreamReader temp = new FileInfo(Path.Combine(srcfolder.FullName, "songs.dta")).OpenText()) {
                    dtaFile = temp.ReadToEnd();
                }
                string[] keys = dtaFile.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                while (false) {}
                return songList;
            } catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
                return null;
			}
        }
    }
}