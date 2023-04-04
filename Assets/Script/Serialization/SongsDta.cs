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
                Encoding dtaEnc = Encoding.GetEncoding("iso-8859-1"); // HMX chose Latin-1 for Data Array, for some reason
                string dtaFile;
                using (StreamReader temp = new StreamReader(Path.Combine(srcfolder.FullName, "songs.dta"), dtaEnc)) {
                    dtaFile = temp.ReadToEnd();
                }
                string[] tokens = dtaFile.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++) {
                    Debug.Log("token index " + i + " , token " + tokens[i]);
                }
                return songList;
            } catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
                return null;
			}
        }
    }
}