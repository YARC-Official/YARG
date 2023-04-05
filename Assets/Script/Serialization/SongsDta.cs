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
                SongInfo newSong = new SongInfo(srcfolder);
                string songPath = "";
                int nestDepth = 0;
                int localSongId = 0;
                bool inSongSubdef = false;
                for (int i = 0; i < tokens.Length; i++) {
                    Debug.Log("token index " + i + " , token " + tokens[i]);
                    string token = tokens[i];
                    if (token.Contains("(song")) {
                        inSongSubdef = true;
                    }
                    if (token.Contains("(name") && inSongSubdef == false) {
                        newSong.SongName = token.Split('"')[1];
                        Debug.Log("song name: " + newSong.SongName);
                    }
                    if (token.Contains("(name") && inSongSubdef == true) {
                        songPath = token.Split("(name")[1].TrimStart().Replace(")","");
                        Debug.Log("song data location, ignoring extensions: " + songPath);
                    }
                    if (token.Contains("(artist")) {
                        newSong.artistName = token.Split('"')[1];
                        Debug.Log("song artist: " + newSong.artistName);
                    }
                }
                songList.Add(newSong);
                return songList;
            } catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
                return null;
			}
        }
    }
}