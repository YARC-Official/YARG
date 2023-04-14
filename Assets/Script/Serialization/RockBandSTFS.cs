using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using UnityEngine;
using YARG.Data;
using DtxCS;
using DtxCS.DataTypes;
using YARG.Serialization;

namespace YARG.Serialization {
	public static class RockBandSTFS {
		static RockBandSTFS() {}
		public static List<SongInfo> ParseSongsDta(DirectoryInfo srcfolder) {
			try {
				List<SongInfo> songList = new List<SongInfo>();
				Encoding dtaEnc = Encoding.GetEncoding("iso-8859-1"); // "dtxcs reads things properly" so turns out that's a lie perpetuated by big c#
				DataArray dtaTree = new DataArray();
				using (StreamReader temp = new StreamReader(Path.Combine(srcfolder.FullName, "songs.dta"), dtaEnc)) {
					dtaTree = DTX.FromDtaString(temp.ReadToEnd());
				}		

				// parse songs.dta for all the songs and their info
				List<XboxSongData> parsedSongs = new List<XboxSongData>();
				for(int i = 0; i < dtaTree.Count; i++){
					XboxSongData currentSong = new XboxSongData();
					parsedSongs.Add(currentSong.ParseFromDataArray((DataArray)dtaTree[i]));
				}
				
				// print out each XboxSongData's, well, song data - useful for debugging
				for(int j = 0; j < parsedSongs.Count; j++)
					Debug.Log(parsedSongs[j].ToString());

				// testing mogg parsing
				// string testMogg = srcfolder.ToString() + "/underthebridge/underthebridge.mogg";

				// if(File.Exists(Path.Combine(srcfolder.FullName, testMogg))){
				// 	Debug.Log("neato, mogg exists");
				// 	LEConverter le = new LEConverter();
				// 	byte[] bytes = new byte[4];
				// 	int oggBegin = 0;
				// 	using(FileStream fileStream = new FileStream(testMogg, FileMode.Open)){
				// 		int n = fileStream.Read(bytes, 0, 4); // skip over bytes 0, 1, 2, 3
				// 		n = fileStream.Read(bytes, 0, 4); // because the info we care about is in bytes 4, 5, 6, 7
				// 		oggBegin = le.LEToInt(bytes); // byte array --> int - bytes are in little endian
				// 	}
				// 	Debug.Log($"ogg audio begins at memory address {oggBegin:X8}, or {oggBegin}");
				// }
				// else Debug.Log("kowabummer");

				// testing png_xbox parsing
				string testPng = srcfolder.ToString() + "/underthebridge/gen/underthebridge_keep.png_xbox";
				if(File.Exists(Path.Combine(srcfolder.FullName, testPng))){
					Debug.Log("png exists");
					XboxImage art = new XboxImage(testPng);
					art.ParseImage();
					art.SaveImageToDisk("lol");
				}
				else Debug.Log("nah dawg");

				string testPng3 = srcfolder.ToString() + "/underthebridge/gen/soybomb.bmp_xbox";
				XboxImage art3 = new XboxImage(testPng3);
				art3.ParseImage();
				art3.SaveImageToDisk("lmao");

				string othertest = srcfolder.ToString() + "/underthebridge/gen/hotforteacher_keep.png_xbox";
				XboxImage otherart = new XboxImage(othertest);
				otherart.ParseImage();
				otherart.SaveImageToDisk("asdf");

				return songList;
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
				return null;
			}
		}
	}
}