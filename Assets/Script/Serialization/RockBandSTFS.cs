using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using YARG.Data;
using DtxCS;
using DtxCS.DataTypes;

namespace YARG.Serialization {
	public static class RockBandSTFS {
		static RockBandSTFS() {}
		public static List<SongInfo> ParseSongsDta(DirectoryInfo srcfolder) {
			try {
				List<SongInfo> songList = new List<SongInfo>();
				// Encoding dtaEnc = Encoding.GetEncoding("iso-8859-1"); // dtxcs reads things properly so it's prob fine but just in case we need the old opening method
				DataArray dtaTree = new DataArray();
				using (FileStream str = new FileStream(Path.Combine(srcfolder.FullName, "songs.dta"), FileMode.Open)) {
					dtaTree = DTX.FromDtaStream(str);
				}		
				int rootNodeCount = dtaTree.Count;
				List<DataNode> dtaSongs = new List<DataNode>();
				List<DataArray> dtaSongArrays = new List<DataArray>();
				string[] shortnames = new string[256];
				for (int i = 0; i < rootNodeCount; i++) {
					dtaSongs.Add(dtaTree[i]);
					shortnames[i] = dtaSongs[i].Name;
					Debug.Log($"dtaSongs = {dtaSongs[i].Name}");
					if (dtaSongs[i].Type == DataType.ARRAY) {
						Debug.Log(dtaSongs[i].ToString());
						dtaSongArrays.Add((DataArray)dtaSongs[i]);
					} else Debug.Log("so sad");
				};
				
				
				string songPath = "songs/test/test";
				string songPathGen = "songs/" + songPath.Split("/")[1] + "/gen/" + songPath.Split("/")[2];
				// Mackiloha.IO.SystemInfo sysInfo = new Mackiloha.IO.SystemInfo {
				// 	Version = 25, 
				// 	Platform = Platform.X360, 
				// 	BigEndian = true
				// };
				// var bitmap = new MiloSerializer(sysInfo).ReadFromFile<HMXBitmap>(Path.Combine(Path.Combine(srcfolder.FullName, songPathGen), "_keep.png_xbox"));
				// string tmpFilePath = Path.GetTempFileName() + ".png";
           		// bitmap.SaveAs(sysInfo, tmpFilePath);
				return songList;
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
				return null;
			}
		}
	}
}